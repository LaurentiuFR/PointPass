namespace PointPass.Api;

public static class InvitationCommand
{
    // A null destination means an explicitly selected, redirected memory pipe.
    // Validate this before creating an invitation or opening the database.
    public static string? Destination(string[] arguments, bool outputRedirected)
    {
        int fileIndex = Array.IndexOf(arguments, "--invite-file");
        bool pipe = arguments.Contains("--invite-stdout", StringComparer.Ordinal);
        if (arguments.Count(value => value == "--invite-stdout") > 1 || arguments.Count(value => value == "--invite-file") > 1)
            throw new InvalidOperationException("Select one invitation output mode once.");
        if (pipe)
        {
            if (fileIndex >= 0 || !outputRedirected)
                throw new InvalidOperationException("--invite-stdout requires redirected output and cannot be combined with --invite-file.");
            return null;
        }
        if (fileIndex < 0 || fileIndex + 1 >= arguments.Length || !Path.IsPathFullyQualified(arguments[fileIndex + 1]))
            throw new InvalidOperationException("Use --create-invite with --invite-stdout through a private pipe, or --invite-file /absolute/private/path.");
        return arguments[fileIndex + 1];
    }

    public static async Task Run(PilotDatabase database, string[] arguments)
    {
        string? path = Destination(arguments, Console.IsOutputRedirected);
        string? token = TokenSecurity.CreateToken();
        try
        {
            await using var connection = await database.Source.OpenConnectionAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await using var command = PilotDatabase.Command(connection, transaction, """
                INSERT INTO invitations(id,code_hash,expires_utc,maximum_uses)
                VALUES ($1,$2,now()+interval '24 hours',1)
                """, Guid.NewGuid(), TokenSecurity.Hash(token));
            await command.ExecuteNonQueryAsync();
            if (path != null)
            {
                var fileOptions = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
                if (!OperatingSystem.IsWindows()) fileOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
                await using (var file = new FileStream(path, fileOptions))
                await using (var writer = new StreamWriter(file))
                    await writer.WriteLineAsync(token);
            }
            await transaction.CommitAsync();
            if (path == null)
            {
                // This is a private data channel, not human-readable command output.
                // The caller must capture it in memory, check exit status and never log it.
                await Console.Out.WriteLineAsync(token);
                await Console.Out.FlushAsync();
                await Console.Error.WriteLineAsync("Created one invitation through the redirected private pipe (24-hour expiry).");
            }
            else Console.WriteLine("Created one private invitation (24-hour expiry). Read the protected file locally; do not add it to logs or Git.");
        }
        finally { token = null; }
    }
}
