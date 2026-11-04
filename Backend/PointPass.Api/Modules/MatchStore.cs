using Npgsql;

namespace PointPass.Api.Modules;

public sealed class MatchStore(PilotDatabase database, PilotOptions options)
{
    public async Task<TicketReply> Join(Guid playerId, RoomJoin request, CancellationToken cancellation)
    {
        CheckVersion(request.GameVersion);
        await using var connection = await database.Source.OpenConnectionAsync(cancellation);
        await using var transaction = await connection.BeginTransactionAsync(cancellation);
        Guid instance = await LockWorker(connection, transaction, null, true, cancellation);
        Guid matchId;
        string state;
        await using (var current = PilotDatabase.Command(connection, transaction,
            "SELECT id, state FROM matches WHERE state IN ('waiting','active') FOR UPDATE"))
        await using (var reader = await current.ExecuteReaderAsync(cancellation))
        {
            if (await reader.ReadAsync(cancellation)) { matchId = reader.GetGuid(0); state = reader.GetString(1); }
            else { matchId = Guid.Empty; state = "waiting"; }
        }
        if (matchId == Guid.Empty)
        {
            matchId = Guid.NewGuid();
            await using var create = PilotDatabase.Command(connection, transaction,
                "INSERT INTO matches(id,worker_instance,game_version,state) VALUES ($1,$2,$3,'waiting')", matchId, instance, options.GameVersion);
            await create.ExecuteNonQueryAsync(cancellation);
        }

        // Unused reservations are bounded and expire. An actual disconnected player keeps
        // their slot for the match worker's 30-second reconnection window.
        if (state == "waiting")
        {
            await using var clean = PilotDatabase.Command(connection, transaction,
                "DELETE FROM match_players WHERE match_id=$1 AND NOT ever_connected AND reserved_until<=now()", matchId);
            await clean.ExecuteNonQueryAsync(cancellation);
        }
        int slot = -1;
        var occupied = new bool[2];
        await using (var players = PilotDatabase.Command(connection, transaction,
            "SELECT player_id,slot,connected,disconnected_utc FROM match_players WHERE match_id=$1", matchId))
        await using (var reader = await players.ExecuteReaderAsync(cancellation))
            while (await reader.ReadAsync(cancellation))
            {
                int candidate = reader.GetInt16(1);
                occupied[candidate] = true;
                if (reader.GetGuid(0) != playerId) continue;
                if (reader.GetBoolean(2)) throw new ApiFault(409, "already_connected", "This guest is already connected.");
                if (!reader.IsDBNull(3) && reader.GetDateTime(3) < DateTime.UtcNow.AddSeconds(-30))
                    throw new ApiFault(409, "reconnect_expired", "The reconnection window has ended. Wait for the room to reset.");
                slot = candidate;
            }
        if (slot < 0)
        {
            if (state != "waiting" || (occupied[0] && occupied[1]))
                throw new ApiFault(409, "server_full", "The private test room is full.");
            slot = occupied[0] ? 1 : 0;
            await using var player = PilotDatabase.Command(connection, transaction, """
                INSERT INTO match_players(match_id,player_id,slot,reserved_until)
                VALUES ($1,$2,$3,now()+interval '30 seconds')
                """, matchId, playerId, (short)slot);
            await player.ExecuteNonQueryAsync(cancellation);
        }
        string token = TokenSecurity.CreateToken();
        DateTime expiry = DateTime.UtcNow.AddSeconds(30);
        await using (var revoke = PilotDatabase.Command(connection, transaction,
            "DELETE FROM match_tickets WHERE match_id=$1 AND player_id=$2", matchId, playerId))
            await revoke.ExecuteNonQueryAsync(cancellation);
        await using (var reserve = PilotDatabase.Command(connection, transaction,
            "UPDATE match_players SET reserved_until=$3 WHERE match_id=$1 AND player_id=$2", matchId, playerId, expiry))
            await reserve.ExecuteNonQueryAsync(cancellation);
        await using (var ticket = PilotDatabase.Command(connection, transaction, """
            INSERT INTO match_tickets(token_hash,match_id,player_id,worker_instance,game_version,expires_utc)
            VALUES ($1,$2,$3,$4,$5,$6)
            """, TokenSecurity.Hash(token), matchId, playerId, instance, options.GameVersion, expiry))
            await ticket.ExecuteNonQueryAsync(cancellation);
        await transaction.CommitAsync(cancellation);
        return new TicketReply(matchId, playerId, slot, token, expiry, options.GameHost, options.GamePort, options.TlsServerName);
    }

    public async Task Leave(Guid playerId, RoomLeave request, CancellationToken cancellation)
    {
        if (request.MatchId == Guid.Empty) throw ApiFault.Invalid();
        await using var connection = await database.Source.OpenConnectionAsync(cancellation);
        await using var transaction = await connection.BeginTransactionAsync(cancellation);
        await LockWorker(connection, transaction, null, false, cancellation);
        await using (var tickets = PilotDatabase.Command(connection, transaction,
            "DELETE FROM match_tickets WHERE match_id=$1 AND player_id=$2", request.MatchId, playerId))
            await tickets.ExecuteNonQueryAsync(cancellation);
        await using (var reservation = PilotDatabase.Command(connection, transaction,
            "DELETE FROM match_players WHERE match_id=$1 AND player_id=$2 AND NOT ever_connected", request.MatchId, playerId))
            await reservation.ExecuteNonQueryAsync(cancellation);
        // The authoritative worker observes the network disconnect and owns game cleanup.
        await transaction.CommitAsync(cancellation);
    }

    public async Task<WorkerReply> Register(WorkerRegister request, CancellationToken cancellation)
    {
        CheckVersion(request.GameVersion);
        if (request.InstanceId == Guid.Empty) throw ApiFault.Invalid();
        await using var connection = await database.Source.OpenConnectionAsync(cancellation);
        await using var transaction = await connection.BeginTransactionAsync(cancellation);
        await using (var singleton = PilotDatabase.Command(connection, transaction,
            "SELECT pg_advisory_xact_lock(706176601)"))
            await singleton.ExecuteNonQueryAsync(cancellation);
        Guid old = Guid.Empty;
        await using (var read = PilotDatabase.Command(connection, transaction,
            "SELECT instance_id FROM workers WHERE singleton FOR UPDATE"))
            if (await read.ExecuteScalarAsync(cancellation) is Guid id) old = id;
        if (old != request.InstanceId)
        {
            await using var abandon = PilotDatabase.Command(connection, transaction, """
                UPDATE matches SET state='abandoned',finished_utc=now(),finish_reason='worker_restart'
                WHERE state IN ('waiting','active')
                """);
            await abandon.ExecuteNonQueryAsync(cancellation);
        }
        await using (var register = PilotDatabase.Command(connection, transaction, """
            INSERT INTO workers(singleton,instance_id,game_version,heartbeat_utc) VALUES (true,$1,$2,now())
            ON CONFLICT(singleton) DO UPDATE SET instance_id=excluded.instance_id,
            game_version=excluded.game_version,heartbeat_utc=now()
            """, request.InstanceId, options.GameVersion))
            await register.ExecuteNonQueryAsync(cancellation);
        WorkerReply reply = await State(connection, transaction, request.InstanceId, cancellation);
        await transaction.CommitAsync(cancellation);
        return reply;
    }

    public async Task<WorkerReply> Heartbeat(WorkerHeartbeat request, CancellationToken cancellation)
    {
        await using var connection = await database.Source.OpenConnectionAsync(cancellation);
        await using var transaction = await connection.BeginTransactionAsync(cancellation);
        await LockWorker(connection, transaction, request.InstanceId, false, cancellation);
        await using (var update = PilotDatabase.Command(connection, transaction,
            "UPDATE workers SET heartbeat_utc=now() WHERE singleton"))
            await update.ExecuteNonQueryAsync(cancellation);
        WorkerReply reply = await State(connection, transaction, request.InstanceId, cancellation);
        await transaction.CommitAsync(cancellation);
        return reply;
    }

    public async Task<AdmissionReply> Redeem(TicketRedeem request, CancellationToken cancellation)
    {
        CheckVersion(request.GameVersion);
        byte[] hash = TokenSecurity.Hash(request.Ticket);
        if (request.ConnectionId.Length is < 1 or > 24 || request.ConnectionId.Any(c => !char.IsAsciiDigit(c)))
            throw ApiFault.Invalid();
        await using var connection = await database.Source.OpenConnectionAsync(cancellation);
        await using var transaction = await connection.BeginTransactionAsync(cancellation);
        await LockWorker(connection, transaction, request.InstanceId, false, cancellation);
        Guid matchId;
        Guid playerId;
        int slot;
        await using (var find = PilotDatabase.Command(connection, transaction, """
            SELECT t.match_id,t.player_id,mp.slot
            FROM match_tickets t JOIN matches m ON m.id=t.match_id
            JOIN match_players mp ON mp.match_id=t.match_id AND mp.player_id=t.player_id
            WHERE t.token_hash=$1 AND t.used_utc IS NULL AND t.expires_utc>now()
              AND t.worker_instance=$2 AND t.game_version=$3 AND m.state IN ('waiting','active')
              AND m.worker_instance=$2 AND NOT mp.connected
              AND (mp.disconnected_utc IS NULL OR mp.disconnected_utc>now()-interval '30 seconds')
            FOR UPDATE OF t,mp,m
            """, hash, request.InstanceId, options.GameVersion))
        await using (var reader = await find.ExecuteReaderAsync(cancellation))
        {
            if (!await reader.ReadAsync(cancellation))
                throw new ApiFault(403, "invalid_ticket", "The match ticket is invalid, expired or already used.");
            matchId = reader.GetGuid(0); playerId = reader.GetGuid(1); slot = reader.GetInt16(2);
        }
        await using (var consume = PilotDatabase.Command(connection, transaction,
            "UPDATE match_tickets SET used_utc=now() WHERE token_hash=$1", hash))
            await consume.ExecuteNonQueryAsync(cancellation);
        await using (var connect = PilotDatabase.Command(connection, transaction, """
            UPDATE match_players SET connected=true,ever_connected=true,connection_id=$3,disconnected_utc=NULL
            WHERE match_id=$1 AND player_id=$2
            """, matchId, playerId, request.ConnectionId))
            await connect.ExecuteNonQueryAsync(cancellation);
        await using (var activate = PilotDatabase.Command(connection, transaction, """
            UPDATE matches SET state='active',started_utc=coalesce(started_utc,now()) WHERE id=$1
            AND (SELECT count(*) FROM match_players WHERE match_id=$1 AND connected)=2
            """, matchId))
            await activate.ExecuteNonQueryAsync(cancellation);
        await transaction.CommitAsync(cancellation);
        return new AdmissionReply(matchId, playerId, slot);
    }

    public async Task Disconnect(PlayerDisconnect request, CancellationToken cancellation)
    {
        if (request.ConnectionId.Length is < 1 or > 24) throw ApiFault.Invalid();
        await using var connection = await database.Source.OpenConnectionAsync(cancellation);
        await using var transaction = await connection.BeginTransactionAsync(cancellation);
        await LockWorker(connection, transaction, request.InstanceId, false, cancellation);
        // Connection identity prevents a delayed old disconnect from ejecting a rejoined player.
        await using var update = PilotDatabase.Command(connection, transaction, """
            UPDATE match_players mp SET connected=false,disconnected_utc=now(),connection_id=NULL
            FROM matches m WHERE mp.match_id=m.id AND m.id=$1 AND mp.player_id=$2
            AND m.worker_instance=$3 AND mp.connection_id=$4
            """, request.MatchId, request.PlayerId, request.InstanceId, request.ConnectionId);
        await update.ExecuteNonQueryAsync(cancellation);
        await transaction.CommitAsync(cancellation);
    }

    public async Task Result(MatchResult request, CancellationToken cancellation)
    {
        if (request.BlueScore is < 0 or > 999 || request.RedScore is < 0 or > 999 || request.MatchId == Guid.Empty)
            throw ApiFault.Invalid();
        await using var connection = await database.Source.OpenConnectionAsync(cancellation);
        await using var transaction = await connection.BeginTransactionAsync(cancellation);
        await LockWorker(connection, transaction, request.InstanceId, false, cancellation);
        string state;
        int blue = -1, red = -1;
        await using (var read = PilotDatabase.Command(connection, transaction,
            "SELECT state,blue_score,red_score FROM matches WHERE id=$1 AND worker_instance=$2 FOR UPDATE", request.MatchId, request.InstanceId))
        await using (var reader = await read.ExecuteReaderAsync(cancellation))
        {
            if (!await reader.ReadAsync(cancellation)) throw new ApiFault(404, "match_missing", "Match not found.");
            state = reader.GetString(0);
            if (!reader.IsDBNull(1)) blue = reader.GetInt32(1);
            if (!reader.IsDBNull(2)) red = reader.GetInt32(2);
        }
        if (state == "completed")
        {
            if (blue != request.BlueScore || red != request.RedScore)
                throw new ApiFault(409, "result_conflict", "A different verified result already exists.");
        }
        else
        {
            if (state != "active") throw new ApiFault(409, "match_not_active", "Only an active match can complete.");
            await using var save = PilotDatabase.Command(connection, transaction, """
                UPDATE matches SET state='completed',blue_score=$2,red_score=$3,finished_utc=now(),finish_reason='full_time'
                WHERE id=$1
                """, request.MatchId, request.BlueScore, request.RedScore);
            await save.ExecuteNonQueryAsync(cancellation);
        }
        await using (var tickets = PilotDatabase.Command(connection, transaction,
            "DELETE FROM match_tickets WHERE match_id=$1", request.MatchId))
            await tickets.ExecuteNonQueryAsync(cancellation);
        await transaction.CommitAsync(cancellation);
    }

    public async Task Abandon(MatchAbandon request, CancellationToken cancellation)
    {
        if (request.MatchId == Guid.Empty || request.Reason.Length is < 1 or > 32 ||
            request.Reason.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_')) throw ApiFault.Invalid();
        await using var connection = await database.Source.OpenConnectionAsync(cancellation);
        await using var transaction = await connection.BeginTransactionAsync(cancellation);
        await LockWorker(connection, transaction, request.InstanceId, false, cancellation);
        await using (var update = PilotDatabase.Command(connection, transaction, """
            UPDATE matches SET state='abandoned',finished_utc=now(),finish_reason=$3
            WHERE id=$1 AND worker_instance=$2 AND state IN ('waiting','active')
            """, request.MatchId, request.InstanceId, request.Reason))
            await update.ExecuteNonQueryAsync(cancellation);
        await using (var tickets = PilotDatabase.Command(connection, transaction,
            "DELETE FROM match_tickets WHERE match_id=$1", request.MatchId))
            await tickets.ExecuteNonQueryAsync(cancellation);
        await transaction.CommitAsync(cancellation);
    }

    private void CheckVersion(string version)
    {
        if (!string.Equals(version, options.GameVersion, StringComparison.Ordinal)) throw ApiFault.Version();
    }

    private static async Task<Guid> LockWorker(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid? expected, bool requireAlive, CancellationToken cancellation)
    {
        await using var read = PilotDatabase.Command(connection, transaction,
            "SELECT instance_id,heartbeat_utc FROM workers WHERE singleton FOR UPDATE");
        await using var reader = await read.ExecuteReaderAsync(cancellation);
        if (!await reader.ReadAsync(cancellation)) throw ApiFault.Unavailable();
        Guid id = reader.GetGuid(0);
        if (expected.HasValue && expected.Value != id) throw new ApiFault(409, "worker_replaced", "The match worker was replaced.");
        if (requireAlive && reader.GetDateTime(1) < DateTime.UtcNow.AddSeconds(-30)) throw ApiFault.Unavailable();
        return id;
    }

    private static async Task<WorkerReply> State(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid instanceId, CancellationToken cancellation)
    {
        Guid? matchId = null;
        string state = "idle";
        await using (var current = PilotDatabase.Command(connection, transaction,
            "SELECT id,state FROM matches WHERE worker_instance=$1 AND state IN ('waiting','active')", instanceId))
        await using (var reader = await current.ExecuteReaderAsync(cancellation))
            if (await reader.ReadAsync(cancellation)) { matchId = reader.GetGuid(0); state = reader.GetString(1); }
        var players = new List<WorkerPlayer>(2);
        if (matchId.HasValue)
        {
            await using var current = PilotDatabase.Command(connection, transaction,
                "SELECT player_id,slot,connected,connection_id FROM match_players WHERE match_id=$1 ORDER BY slot", matchId.Value);
            await using var reader = await current.ExecuteReaderAsync(cancellation);
            while (await reader.ReadAsync(cancellation))
                players.Add(new WorkerPlayer(reader.GetGuid(0), reader.GetInt16(1), reader.GetBoolean(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3)));
        }
        return new WorkerReply(instanceId, matchId, state, players.ToArray());
    }
}
