using Npgsql;

namespace PointPass.Api;

public sealed class DatabaseMaintenance(PilotDatabase database, ILogger<DatabaseMaintenance> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var command = database.Source.CreateCommand("""
                    DELETE FROM access_sessions WHERE expires_utc<now();
                    DELETE FROM invitations WHERE expires_utc<now()-interval '1 day';
                    DELETE FROM match_tickets WHERE expires_utc<now()-interval '1 day';
                    """);
                await command.ExecuteNonQueryAsync(stoppingToken);
            }
            catch (NpgsqlException)
            {
                // No connection strings, query parameters, tokens or exception details in logs.
                logger.LogWarning("Database maintenance deferred; database temporarily unavailable.");
            }
        }
    }
}
