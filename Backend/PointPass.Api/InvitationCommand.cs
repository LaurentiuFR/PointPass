namespace PointPass.Api;

public static class InvitationCommand
{
    public static async Task Run(PilotDatabase database, string[] arguments)
    {
        int fileIndex = Array.IndexOf(arguments, "--invite-file");
        if (fileIndex < 0 || fileIndex + 1 >= arguments.Length || !Path.IsPathFullyQualified(arguments[fileIndex + 1]))
            throw new InvalidOperationException("Use --create-invite --invite-file /absolute/private/path (new file only).");
        string path = arguments[fileIndex + 1];
        string token = TokenSecurity.CreateToken();
        await using var connection = await database.Source.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = PilotDatabase.Command(connection, transaction, """
            INSERT INTO invitations(id,code_hash,expires_utc,maximum_uses)
            VALUES ($1,$2,now()+interval '24 hours',1)
            """, Guid.NewGuid(), TokenSecurity.Hash(token));
        await command.ExecuteNonQueryAsync();
        var fileOptions = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
        if (!OperatingSystem.IsWindows()) fileOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        await using (var file = new FileStream(path, fileOptions))
        await using (var writer = new StreamWriter(file))
            await writer.WriteLineAsync(token);
        await transaction.CommitAsync();
        Console.WriteLine("Created one private invitation (24-hour expiry). Read the protected file locally; do not add it to logs or Git.");
    }
}
