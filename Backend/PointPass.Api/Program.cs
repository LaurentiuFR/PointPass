using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Npgsql;
using PointPass.Api;
using PointPass.Api.Modules;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(server =>
{
    server.AddServerHeader = false;
    server.Limits.MaxRequestBodySize = 8192;
    server.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(10);
    server.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(30);
});
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options => options.SingleLine = true);
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Information);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
    options.SerializerOptions.RespectNullableAnnotations = true;
    options.SerializerOptions.RespectRequiredConstructorParameters = true;
    options.SerializerOptions.MaxDepth = 8;
    options.SerializerOptions.AllowDuplicateProperties = false;
});
var options = builder.Configuration.GetSection("PointPass").Get<PilotOptions>() ?? new PilotOptions();
options.Validate();
string connectionString = builder.Configuration.GetConnectionString("PointPass")
    ?? throw new InvalidOperationException("Configure ConnectionStrings:PointPass using protected server configuration.");
builder.Services.AddSingleton(options);
builder.Services.AddSingleton(new PilotDatabase(connectionString));
builder.Services.AddSingleton<GuestStore>();
builder.Services.AddSingleton<MatchStore>();
builder.Services.AddSingleton<SecurityCounters>();
builder.Services.AddHostedService<DatabaseMaintenance>();
builder.Services.Configure<ForwardedHeadersOptions>(forward =>
{
    forward.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    forward.ForwardLimit = 1;
    forward.KnownProxies.Clear();
    forward.KnownIPNetworks.Clear();
    forward.KnownProxies.Add(IPAddress.Loopback);
});
builder.Services.AddRateLimiter(limits =>
{
    limits.RejectionStatusCode = 429;
    limits.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ =>
            new FixedWindowRateLimiterOptions { PermitLimit = 90, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
    limits.AddPolicy("guest", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ =>
            new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
    limits.OnRejected = async (context, cancellation) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await context.HttpContext.Response.WriteAsJsonAsync(new { code = "rate_limited", message = "Too many requests. Wait a minute and try again." }, cancellation);
    };
});

await using var app = builder.Build();
if (args.Contains("--create-invite", StringComparer.Ordinal))
{
    await InvitationCommand.Run(app.Services.GetRequiredService<PilotDatabase>(), args);
    return;
}
app.UseForwardedHeaders();
app.Use(async (context, next) =>
{
    try
    {
        context.Response.Headers.CacheControl = "no-store";
        if (context.Request.QueryString.HasValue) throw ApiFault.Invalid();
        await next();
        if (context.Response.StatusCode >= 400)
            context.RequestServices.GetRequiredService<SecurityCounters>().Reject(context.Response.StatusCode);
    }
    catch (Exception error) when (error is ApiFault or BadHttpRequestException or JsonException or NpgsqlException or TimeoutException)
    {
        if (context.Response.HasStarted) throw;
        int status = error is ApiFault fault ? fault.Status : error is BadHttpRequestException or JsonException ? 400 : 503;
        string code = error is ApiFault api ? api.Code : status == 400 ? "invalid_request" : "temporarily_unavailable";
        string message = error is ApiFault known ? known.Message : status == 400 ? "Check the supplied request fields." : "The test service is temporarily unavailable.";
        context.RequestServices.GetRequiredService<SecurityCounters>().Reject(status);
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new { code, message }, context.RequestAborted);
    }
});
app.UseRateLimiter();

app.MapGet("/health", () => Results.Ok(new { status = "ok", protocol = options.GameVersion }));
app.MapPost("/v1/guests", async (GuestCreate request, GuestStore guests, CancellationToken cancellation) =>
    Results.Ok(await guests.Register(request, cancellation))).RequireRateLimiting("guest");
app.MapPost("/v1/guests/session", async (GuestLogin request, GuestStore guests, CancellationToken cancellation) =>
    Results.Ok(await guests.Login(request, cancellation))).RequireRateLimiting("guest");
app.MapGet("/v1/profile", async (HttpContext context, GuestStore guests, CancellationToken cancellation) =>
    Results.Ok(await guests.Profile(await guests.Authenticate(context, cancellation), cancellation)));
app.MapGet("/v1/history", async (HttpContext context, GuestStore guests, CancellationToken cancellation) =>
    Results.Ok(await guests.History(await guests.Authenticate(context, cancellation), cancellation)));
app.MapPost("/v1/room/join", async (RoomJoin request, HttpContext context, GuestStore guests, MatchStore matches, CancellationToken cancellation) =>
    Results.Ok(await matches.Join(await guests.Authenticate(context, cancellation), request, cancellation)));
app.MapPost("/v1/room/leave", async (RoomLeave request, HttpContext context, GuestStore guests, MatchStore matches, CancellationToken cancellation) =>
{
    await matches.Leave(await guests.Authenticate(context, cancellation), request, cancellation);
    return Results.NoContent();
});

var worker = app.MapGroup("/internal");
worker.AddEndpointFilter(async (context, next) =>
{
    if (context.HttpContext.Connection.RemoteIpAddress is not { } address || !IPAddress.IsLoopback(address) ||
        !TokenSecurity.SecretEquals(context.HttpContext.Request.Headers["X-PointPass-Worker-Key"].ToString(), options.WorkerKey))
        throw new ApiFault(403, "forbidden", "Access denied.");
    return await next(context);
});
worker.MapGet("/security-counters", (SecurityCounters counters) => Results.Ok(counters.Snapshot()));
worker.MapPost("/worker/register", async (WorkerRegister request, MatchStore matches, CancellationToken cancellation) =>
    Results.Ok(await matches.Register(request, cancellation)));
worker.MapPost("/worker/heartbeat", async (WorkerHeartbeat request, MatchStore matches, CancellationToken cancellation) =>
    Results.Ok(await matches.Heartbeat(request, cancellation)));
worker.MapPost("/tickets/redeem", async (TicketRedeem request, MatchStore matches, CancellationToken cancellation) =>
    Results.Ok(await matches.Redeem(request, cancellation)));
worker.MapPost("/players/disconnect", async (PlayerDisconnect request, MatchStore matches, CancellationToken cancellation) =>
{
    await matches.Disconnect(request, cancellation); return Results.NoContent();
});
worker.MapPost("/matches/result", async (MatchResult request, MatchStore matches, CancellationToken cancellation) =>
{
    await matches.Result(request, cancellation); return Results.Ok(new { saved = true });
});
worker.MapPost("/matches/abandon", async (MatchAbandon request, MatchStore matches, CancellationToken cancellation) =>
{
    await matches.Abandon(request, cancellation); return Results.NoContent();
});
await app.RunAsync();
