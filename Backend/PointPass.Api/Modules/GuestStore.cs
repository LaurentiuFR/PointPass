using Npgsql;

namespace PointPass.Api.Modules;

public sealed class GuestStore(PilotDatabase database)
{
    public async Task<SessionReply> Register(GuestCreate request, CancellationToken cancellation)
    {
        byte[] credential = TokenSecurity.Hash(request.Credential);
        byte[] invitation = TokenSecurity.Hash(request.Invitation);
        string name = TokenSecurity.DisplayName(request.DisplayName);
        await using var connection = await database.Source.OpenConnectionAsync(cancellation);
        await using var transaction = await connection.BeginTransactionAsync(cancellation);

        // A device writes its random credential before registration. Retrying a lost response
        // reuses the same guest rather than consuming another invitation or losing identity.
        Guid? existing = await FindPlayer(connection, transaction, credential, cancellation);
        if (existing is null)
        {
            await using var invite = PilotDatabase.Command(connection, transaction,
                "UPDATE invitations SET used = used + 1 WHERE code_hash = $1 AND expires_utc > now() AND used < maximum_uses RETURNING id", invitation);
            if (await invite.ExecuteScalarAsync(cancellation) is null)
                throw new ApiFault(403, "invitation_required", "A valid, unused private-pilot invitation is required.");
            existing = Guid.NewGuid();
            await using var create = PilotDatabase.Command(connection, transaction,
                "INSERT INTO players(id, credential_hash, display_name) VALUES ($1, $2, $3)", existing.Value, credential, name);
            await create.ExecuteNonQueryAsync(cancellation);
        }
        SessionReply reply = await NewSession(connection, transaction, existing.Value, cancellation);
        await transaction.CommitAsync(cancellation);
        return reply;
    }

    public async Task<SessionReply> Login(GuestLogin request, CancellationToken cancellation)
    {
        byte[] credential = TokenSecurity.Hash(request.Credential);
        await using var connection = await database.Source.OpenConnectionAsync(cancellation);
        await using var transaction = await connection.BeginTransactionAsync(cancellation);
        Guid? id = await FindPlayer(connection, transaction, credential, cancellation);
        if (id is null) throw ApiFault.Unauthorized();
        SessionReply reply = await NewSession(connection, transaction, id.Value, cancellation);
        await transaction.CommitAsync(cancellation);
        return reply;
    }

    public async Task<Guid> Authenticate(HttpContext context, CancellationToken cancellation)
    {
        string authorization = context.Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.Ordinal) || authorization.Length != 50)
            throw ApiFault.Unauthorized();
        byte[] hash;
        try { hash = TokenSecurity.Hash(authorization[7..]); }
        catch (ApiFault) { throw ApiFault.Unauthorized(); }
        await using var command = database.Source.CreateCommand(
            "SELECT player_id FROM access_sessions WHERE token_hash = $1 AND expires_utc > now()");
        command.Parameters.AddWithValue(hash);
        return await command.ExecuteScalarAsync(cancellation) is Guid id ? id : throw ApiFault.Unauthorized();
    }

    public async Task<object> Profile(Guid playerId, CancellationToken cancellation)
    {
        await using var command = database.Source.CreateCommand("""
            SELECT p.display_name, p.created_utc,
              count(m.id)::integer AS played,
              count(m.id) FILTER (WHERE (mp.slot=0 AND m.blue_score>m.red_score) OR (mp.slot=1 AND m.red_score>m.blue_score))::integer AS wins,
              count(m.id) FILTER (WHERE m.blue_score=m.red_score)::integer AS draws,
              count(m.id) FILTER (WHERE (mp.slot=0 AND m.blue_score<m.red_score) OR (mp.slot=1 AND m.red_score<m.blue_score))::integer AS losses
            FROM players p
            LEFT JOIN match_players mp ON mp.player_id=p.id
            LEFT JOIN matches m ON m.id=mp.match_id AND m.state='completed'
            WHERE p.id=$1 GROUP BY p.id
            """);
        command.Parameters.AddWithValue(playerId);
        await using var reader = await command.ExecuteReaderAsync(cancellation);
        if (!await reader.ReadAsync(cancellation)) throw ApiFault.Unauthorized();
        return new { playerId, displayName = reader.GetString(0), createdUtc = reader.GetDateTime(1),
            played = reader.GetInt32(2), wins = reader.GetInt32(3), draws = reader.GetInt32(4), losses = reader.GetInt32(5) };
    }

    public async Task<object[]> History(Guid playerId, CancellationToken cancellation)
    {
        await using var command = database.Source.CreateCommand("""
            SELECT m.id, mp.slot, m.blue_score, m.red_score, m.finished_utc
            FROM matches m JOIN match_players mp ON mp.match_id=m.id
            WHERE mp.player_id=$1 AND m.state='completed'
            ORDER BY m.finished_utc DESC LIMIT 20
            """);
        command.Parameters.AddWithValue(playerId);
        await using var reader = await command.ExecuteReaderAsync(cancellation);
        var records = new List<object>(20);
        while (await reader.ReadAsync(cancellation))
            records.Add(new { matchId = reader.GetGuid(0), slot = reader.GetInt16(1), blueScore = reader.GetInt32(2),
                redScore = reader.GetInt32(3), finishedUtc = reader.GetDateTime(4) });
        return records.ToArray();
    }

    private static async Task<Guid?> FindPlayer(NpgsqlConnection connection, NpgsqlTransaction transaction,
        byte[] credential, CancellationToken cancellation)
    {
        await using var command = PilotDatabase.Command(connection, transaction,
            "SELECT id FROM players WHERE credential_hash = $1 FOR UPDATE", credential);
        return await command.ExecuteScalarAsync(cancellation) is Guid id ? id : null;
    }

    private static async Task<SessionReply> NewSession(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid playerId, CancellationToken cancellation)
    {
        string token = TokenSecurity.CreateToken();
        DateTime expiry = DateTime.UtcNow.AddMinutes(10);
        await using (var clean = PilotDatabase.Command(connection, transaction, """
            DELETE FROM access_sessions WHERE player_id=$1 AND
            (expires_utc<=now() OR token_hash IN
                (SELECT token_hash FROM access_sessions WHERE player_id=$1 ORDER BY created_utc DESC OFFSET 2))
            """, playerId))
            await clean.ExecuteNonQueryAsync(cancellation);
        await using (var create = PilotDatabase.Command(connection, transaction,
            "INSERT INTO access_sessions(token_hash, player_id, expires_utc) VALUES ($1, $2, $3)", TokenSecurity.Hash(token), playerId, expiry))
            await create.ExecuteNonQueryAsync(cancellation);
        await using var name = PilotDatabase.Command(connection, transaction,
            "SELECT display_name FROM players WHERE id=$1", playerId);
        return new SessionReply(playerId, token, expiry, (string)(await name.ExecuteScalarAsync(cancellation))!);
    }
}
