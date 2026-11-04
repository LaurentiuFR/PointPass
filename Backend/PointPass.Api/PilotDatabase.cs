using Npgsql;

namespace PointPass.Api;

public sealed class PilotDatabase : IAsyncDisposable
{
    public NpgsqlDataSource Source { get; }

    public PilotDatabase(string connectionString)
    {
        var settings = new NpgsqlConnectionStringBuilder(connectionString)
        {
            MaxPoolSize = 8, MinPoolSize = 0, Timeout = 3, CommandTimeout = 5,
            IncludeErrorDetail = false, ApplicationName = "pointpass-api"
        };
        Source = NpgsqlDataSource.Create(settings.ConnectionString);
    }

    public static NpgsqlCommand Command(NpgsqlConnection connection, NpgsqlTransaction? transaction,
        string sql, params object[] values)
    {
        var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (object value in values) command.Parameters.Add(new NpgsqlParameter { Value = value });
        return command;
    }

    public ValueTask DisposeAsync() => Source.DisposeAsync();
}
