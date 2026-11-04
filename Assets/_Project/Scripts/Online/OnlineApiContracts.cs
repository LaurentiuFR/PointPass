using System;

namespace PointPass.Online
{
    /// <summary>Pilot API DTOs. These contain no deployment credentials on clients.</summary>
    public static class OnlineApiContracts
    {
        [Serializable] public sealed class GuestRequest { public string invitation, displayName, credential; }
        [Serializable] public sealed class SessionRequest { public string credential; }
        [Serializable] public sealed class Guest { public string playerId, accessToken, expiresUtc, displayName; }
        [Serializable] public sealed class JoinRequest { public string gameVersion = OnlineProtocol.GameVersion; }
        [Serializable] public sealed class Admission { public string matchId, playerId, ticket, expiresUtc, host, tlsServerName; public int slot, port; }
        [Serializable] public sealed class LeaveRequest { public string matchId; }
        [Serializable] public sealed class Error { public string code, message; }
        [Serializable] public sealed class Approval { public string ticket, gameVersion = OnlineProtocol.GameVersion; }
        [Serializable] public sealed class Register { public string instanceId, gameVersion = OnlineProtocol.GameVersion; }
        [Serializable] public sealed class Heartbeat { public string instanceId; }
        [Serializable] public sealed class Redeem { public string instanceId, ticket, gameVersion, connectionId; }
        [Serializable] public sealed class Player { public string playerId, connectionId; public int slot; public bool connected; }
        [Serializable] public sealed class Worker { public string instanceId, matchId, state; public Player[] players; }
        [Serializable] public sealed class Redeemed { public string matchId, playerId; public int slot; }
        [Serializable] public sealed class Departed { public string instanceId, matchId, playerId, connectionId; }
        [Serializable] public sealed class Result { public string instanceId, matchId; public int blueScore, redScore; }
        [Serializable] public sealed class Abandon { public string instanceId, matchId, reason; }
    }
}
