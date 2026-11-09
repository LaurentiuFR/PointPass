using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Npgsql;
using PointPass.Api;

int passed = 0;
void Check(bool condition, string label)
{
    if (!condition) throw new InvalidOperationException("FAILED: " + label);
    passed++;
    Console.WriteLine("PASS " + label);
}
void Invalid(Action operation, string label)
{
    try { operation(); }
    catch (ApiFault) { Check(true, label); return; }
    throw new InvalidOperationException("FAILED: " + label);
}

void InvalidInvitationOutput(string[] arguments, bool redirected, string label)
{
    try { InvitationCommand.Destination(arguments, redirected); }
    catch (InvalidOperationException) { Check(true, label); return; }
    throw new InvalidOperationException("FAILED: " + label);
}
string invitationExamplePath = Path.Combine(Path.GetTempPath(), "pointpass-invitation-example.txt");
Check(InvitationCommand.Destination(new[] { "--create-invite", "--invite-stdout" }, true) == null,
    "explicit redirected invitation pipe has no file destination");
Check(InvitationCommand.Destination(new[] { "--create-invite", "--invite-file", invitationExamplePath }, false) == invitationExamplePath,
    "legacy private file destination remains supported");
InvalidInvitationOutput(new[] { "--create-invite", "--invite-stdout" }, false, "terminal invitation output is refused before minting");
InvalidInvitationOutput(new[] { "--create-invite", "--invite-stdout", "--invite-file", invitationExamplePath }, true,
    "mixed invitation output modes are refused");
InvalidInvitationOutput(new[] { "--create-invite", "--invite-stdout", "--invite-stdout" }, true, "duplicate invitation pipe mode is refused");
InvalidInvitationOutput(new[] { "--create-invite", "--invite-file", "relative-private-file" }, true, "relative invitation file is refused");
InvalidInvitationOutput(new[] { "--create-invite" }, true, "invitation output mode must be explicit");

var tokens = new HashSet<string>(StringComparer.Ordinal);
for (int i = 0; i < 1000; i++)
{
    string token = TokenSecurity.CreateToken();
    if (token.Length != 43 || !tokens.Add(token) || TokenSecurity.Hash(token).Length != 32)
        throw new InvalidOperationException("Random token format/uniqueness failed.");
}
Check(tokens.Count == 1000, "1000 random tokens have expected format, unique values and SHA256 hashes");
Invalid(() => TokenSecurity.Hash(null), "null token rejected");
Invalid(() => TokenSecurity.Hash(""), "empty token rejected");
Invalid(() => TokenSecurity.Hash(new string('x', 44)), "oversized token rejected");
Invalid(() => TokenSecurity.Hash(new string('x', 42) + "'"), "token injection syntax rejected");
Invalid(() => TokenSecurity.DisplayName("<script>"), "HTML display name rejected");
Invalid(() => TokenSecurity.DisplayName(new string('a', 25)), "oversized display name rejected");
Check(TokenSecurity.DisplayName("  Player 1  ") == "Player 1", "display name normalized");
Check(TokenSecurity.SecretEquals("correct", "correct") && !TokenSecurity.SecretEquals("incorrect", "correct") &&
      !TokenSecurity.SecretEquals(null, "correct"), "worker key equality");
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    AllowDuplicateProperties = false,
    MaxDepth = 8
};
foreach (string invalid in new[] { "{}", "{\"credential\":null}", "{\"credential\":\"a\",\"unknown\":true}",
             "{\"credential\":\"a\",\"credential\":\"b\"}" })
{
    try { JsonSerializer.Deserialize<GuestLogin>(invalid, json); }
    catch (JsonException) { Check(true, "strict JSON rejects " + invalid); continue; }
    throw new InvalidOperationException("Strict JSON accepted malformed input.");
}

if (args.Contains("--integration", StringComparer.Ordinal) || args.Contains("--verify-persistence", StringComparer.Ordinal))
{
    string connection = Environment.GetEnvironmentVariable("ConnectionStrings__PointPass")
        ?? throw new InvalidOperationException("Configure the isolated validation database connection.");
    var settings = new NpgsqlConnectionStringBuilder(connection);
    if (settings.Database is null || !settings.Database.EndsWith("_validation", StringComparison.Ordinal) ||
        Environment.GetEnvironmentVariable("POINTPASS_TEST_ALLOW_MUTATION") != "isolated-validation")
        throw new InvalidOperationException("Integration checks require a disposable *_validation database and explicit mutation guard.");
    string url = Environment.GetEnvironmentVariable("POINTPASS_API_TEST_URL") ?? "http://127.0.0.1:5001";
    if (!Uri.TryCreate(url, UriKind.Absolute, out var endpoint) || !IPAddress.TryParse(endpoint.Host, out var ip) || !IPAddress.IsLoopback(ip))
        throw new InvalidOperationException("Run integration checks against a separate loopback API only.");
    string workerKey = Environment.GetEnvironmentVariable("PointPass__WorkerKey")
        ?? throw new InvalidOperationException("Configure the test API worker key.");
    string version = Environment.GetEnvironmentVariable("PointPass__GameVersion") ?? "PointPass-Online-1";
    await using var db = NpgsqlDataSource.Create(connection);
    using var client = new HttpClient { BaseAddress = endpoint, Timeout = TimeSpan.FromSeconds(10) };
    using var worker = new HttpClient { BaseAddress = endpoint, Timeout = TimeSpan.FromSeconds(10) };
    worker.DefaultRequestHeaders.Add("X-PointPass-Worker-Key", workerKey);

    async Task<(HttpStatusCode Status, JsonElement Json)> Request(HttpClient http, string path, object? body = null)
    {
        using var response = body is null ? await http.GetAsync(path) : await http.PostAsJsonAsync(path, body);
        string content = await response.Content.ReadAsStringAsync();
        JsonElement parsed = content.Length == 0 ? JsonSerializer.SerializeToElement(new { }) : JsonDocument.Parse(content).RootElement.Clone();
        return (response.StatusCode, parsed);
    }
    async Task<string> Invitation()
    {
        string invitation = TokenSecurity.CreateToken();
        await using var insert = db.CreateCommand("INSERT INTO invitations(id,code_hash,expires_utc) VALUES ($1,$2,now()+interval '1 hour')");
        insert.Parameters.AddWithValue(Guid.NewGuid()); insert.Parameters.AddWithValue(TokenSecurity.Hash(invitation));
        await insert.ExecuteNonQueryAsync();
        return invitation;
    }
    async Task<(Guid Id, string Credential, string Access)> Guest(string name)
    {
        string credential = TokenSecurity.CreateToken();
        var response = await Request(client, "/v1/guests", new { invitation = await Invitation(), credential, displayName = name });
        Check(response.Status == HttpStatusCode.OK, name + " registered");
        return (response.Json.GetProperty("playerId").GetGuid(), credential, response.Json.GetProperty("accessToken").GetString()!);
    }
    void Authenticate(string access) => client.DefaultRequestHeaders.Authorization = new("Bearer", access);

    string? stateFile = Environment.GetEnvironmentVariable("POINTPASS_TEST_STATE_FILE");
    if (args.Contains("--verify-persistence", StringComparer.Ordinal))
    {
        if (stateFile is null || !Path.IsPathFullyQualified(stateFile) || new FileInfo(stateFile).Length > 4096)
            throw new InvalidOperationException("Configure the protected validation-state file created by --integration.");
        using var saved = JsonDocument.Parse(await File.ReadAllTextAsync(stateFile));
        Guid expectedPlayer = saved.RootElement.GetProperty("playerId").GetGuid();
        var restored = await Request(client, "/v1/guests/session", new { credential = saved.RootElement.GetProperty("credential").GetString() });
        Check(restored.Status == HttpStatusCode.OK && restored.Json.GetProperty("playerId").GetGuid() == expectedPlayer,
            "guest identity persists across service restart");
        Authenticate(restored.Json.GetProperty("accessToken").GetString()!);
        var profile = await Request(client, "/v1/profile");
        Check(profile.Status == HttpStatusCode.OK && profile.Json.GetProperty("wins").GetInt32() == 1,
            "verified result persists across service restart");
        Console.WriteLine($"Completed {passed} checks including post-restart persistence.");
        return;
    }

    Check((await Request(client, "/v1/profile")).Status == HttpStatusCode.Unauthorized, "profile requires authentication");
    Check((await Request(client, "/internal/worker/heartbeat", new { instanceId = Guid.NewGuid() })).Status == HttpStatusCode.Forbidden,
        "internal API rejects absent worker key");
    var blue = await Guest("Blue Test");
    var red = await Guest("Red Test");
    var third = await Guest("Capacity Test");
    Authenticate(blue.Access);
    var blueProfile = await Request(client, "/v1/profile");
    Check(blueProfile.Json.GetProperty("playerId").GetGuid() == blue.Id, "profile identity bound to bearer session");
    Check((await Request(client, "/v1/profile?playerId=" + red.Id)).Status == HttpStatusCode.BadRequest, "profile query impersonation rejected");
    Guid instance = Guid.NewGuid();
    Check((await Request(worker, "/internal/worker/register", new { instanceId = instance, gameVersion = version })).Status == HttpStatusCode.OK,
        "worker registers");
    Check((await Request(client, "/v1/room/join", new { gameVersion = "incompatible" })).Status == HttpStatusCode.Conflict,
        "incompatible version rejected");
    var firstTicket = await Request(client, "/v1/room/join", new { gameVersion = version });
    Check(firstTicket.Status == HttpStatusCode.OK && firstTicket.Json.GetProperty("slot").GetInt32() == 0, "first player reserves blue slot");
    Guid match = firstTicket.Json.GetProperty("matchId").GetGuid();
    string blueTicket = firstTicket.Json.GetProperty("ticket").GetString()!;
    Authenticate(red.Access);
    var secondTicket = await Request(client, "/v1/room/join", new { gameVersion = version });
    Check(secondTicket.Status == HttpStatusCode.OK && secondTicket.Json.GetProperty("slot").GetInt32() == 1,
        "second player reserves opposing slot");
    string redTicket = secondTicket.Json.GetProperty("ticket").GetString()!;
    Authenticate(third.Access);
    Check((await Request(client, "/v1/room/join", new { gameVersion = version })).Status == HttpStatusCode.Conflict, "third player rejected at capacity");
    object BlueAdmission() => new { instanceId = instance, ticket = blueTicket, gameVersion = version, connectionId = "1" };
    // Ignore the successful approval body after the transaction commits. A worker that
    // loses that body must recover the connection identity from its trusted heartbeat.
    using (var redemption = new HttpRequestMessage(HttpMethod.Post, "/internal/tickets/redeem")
           { Content = JsonContent.Create(BlueAdmission()) })
    using (var ignoredApproval = await worker.SendAsync(redemption, HttpCompletionOption.ResponseHeadersRead))
        Check(ignoredApproval.StatusCode == HttpStatusCode.OK, "blue ticket commits while its approval body is deliberately ignored");
    Check((await Request(worker, "/internal/tickets/redeem", BlueAdmission())).Status == HttpStatusCode.Forbidden, "ticket reuse rejected");
    Check((await Request(worker, "/internal/tickets/redeem", new { instanceId = instance, ticket = redTicket, gameVersion = version, connectionId = "2" })).Status == HttpStatusCode.OK,
        "red ticket accepted");
    var activeHeartbeat = await Request(worker, "/internal/worker/heartbeat", new { instanceId = instance });
    Check(activeHeartbeat.Json.GetProperty("state").GetString() == "active", "two authenticated players activate match");
    JsonElement orphan = activeHeartbeat.Json.GetProperty("players")[0];
    Check(orphan.GetProperty("playerId").GetGuid() == blue.Id && orphan.GetProperty("connected").GetBoolean()
        && orphan.GetProperty("connectionId").GetString() == "1",
        "trusted heartbeat recovers the identity of a redemption whose approval body was ignored");
    Check(!firstTicket.Json.TryGetProperty("connectionId", out _), "connection reconciliation identity is absent from public admission replies");
    Check((await Request(worker, "/internal/players/disconnect", new { instanceId = instance,
        matchId = activeHeartbeat.Json.GetProperty("matchId").GetGuid(), playerId = orphan.GetProperty("playerId").GetGuid(),
        connectionId = orphan.GetProperty("connectionId").GetString() })).Status == HttpStatusCode.NoContent,
        "worker reconciles an orphan using only trusted heartbeat fields");
    var disconnectedHeartbeat = await Request(worker, "/internal/worker/heartbeat", new { instanceId = instance });
    Check(!disconnectedHeartbeat.Json.GetProperty("players")[0].GetProperty("connected").GetBoolean()
        && disconnectedHeartbeat.Json.GetProperty("players")[0].GetProperty("connectionId").ValueKind == JsonValueKind.Null,
        "reconciliation clears the orphaned connection and exposes no stale connection identity");
    Authenticate(blue.Access);
    var rejoin = await Request(client, "/v1/room/join", new { gameVersion = version });
    Check(rejoin.Status == HttpStatusCode.OK && rejoin.Json.GetProperty("slot").GetInt32() == 0, "same guest reclaims original slot");
    string rejoinTicket = rejoin.Json.GetProperty("ticket").GetString()!;
    Check((await Request(worker, "/internal/tickets/redeem", new { instanceId = instance, ticket = rejoinTicket, gameVersion = version, connectionId = "3" })).Status == HttpStatusCode.OK,
        "rejoin ticket accepted");
    await Request(worker, "/internal/players/disconnect", new { instanceId = instance, matchId = match, playerId = blue.Id, connectionId = "1" });
    var heartbeat = await Request(worker, "/internal/worker/heartbeat", new { instanceId = instance });
    Check(heartbeat.Json.GetProperty("players")[0].GetProperty("connected").GetBoolean()
        && heartbeat.Json.GetProperty("players")[0].GetProperty("connectionId").GetString() == "3",
        "stale disconnect cannot eject or overwrite the new connection identity");
    var result = new { instanceId = instance, matchId = match, blueScore = 2, redScore = 1 };
    Check((await Request(worker, "/internal/matches/result", result)).Status == HttpStatusCode.OK, "trusted result saved");
    Check((await Request(worker, "/internal/matches/result", result)).Status == HttpStatusCode.OK, "duplicate result idempotent");
    Check((await Request(worker, "/internal/matches/result", new { instanceId = instance, matchId = match, blueScore = 3, redScore = 1 })).Status == HttpStatusCode.Conflict,
        "conflicting repeated result rejected");
    Check((await Request(client, "/v1/profile")).Json.GetProperty("wins").GetInt32() == 1, "duplicate result counted once");
    Check((await Request(client, "/v1/history")).Json.GetArrayLength() == 1, "verified match in history");
    Check((await Request(client, "/internal/matches/result", result)).Status == HttpStatusCode.Forbidden, "player cannot submit authoritative result");
    var login = await Request(client, "/v1/guests/session", new { credential = blue.Credential });
    Check(login.Status == HttpStatusCode.OK && login.Json.GetProperty("playerId").GetGuid() == blue.Id, "persistent guest credential restores identity");

    // Expiry is tested by changing the disposable database, avoiding a slow wall-clock wait.
    Authenticate(blue.Access);
    var expiryTicket = await Request(client, "/v1/room/join", new { gameVersion = version });
    await using (var expire = db.CreateCommand("UPDATE match_tickets SET expires_utc=now()-interval '1 second' WHERE token_hash=$1"))
    {
        expire.Parameters.AddWithValue(TokenSecurity.Hash(expiryTicket.Json.GetProperty("ticket").GetString()!));
        await expire.ExecuteNonQueryAsync();
    }
    Check((await Request(worker, "/internal/tickets/redeem", new { instanceId = instance,
        ticket = expiryTicket.Json.GetProperty("ticket").GetString(), gameVersion = version, connectionId = "4" })).Status == HttpStatusCode.Forbidden,
        "expired ticket rejected");
    using var malformed = await client.PostAsync("/v1/room/join", new StringContent("{\"gameVersion\":\"x\",\"score\":999}", Encoding.UTF8, "application/json"));
    Check(malformed.StatusCode == HttpStatusCode.BadRequest, "unknown authoritative field rejected");
    if (stateFile is not null)
    {
        if (!Path.IsPathFullyQualified(stateFile)) throw new InvalidOperationException("Validation-state path must be absolute and private.");
        var fileOptions = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
        if (!OperatingSystem.IsWindows()) fileOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        await using var file = new FileStream(stateFile, fileOptions);
        await JsonSerializer.SerializeAsync(file, new { playerId = blue.Id, credential = blue.Credential });
        Console.WriteLine("Private persistence fixture written; restart the isolated API and rerun --verify-persistence.");
    }
    Console.WriteLine("Integration suite used only the guarded disposable database. No game-transport validation is implied.");
}
Console.WriteLine($"Completed {passed} checks.");
