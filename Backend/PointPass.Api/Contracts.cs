namespace PointPass.Api;

public sealed record GuestCreate(string Invitation, string Credential, string? DisplayName);
public sealed record GuestLogin(string Credential);
public sealed record SessionReply(Guid PlayerId, string AccessToken, DateTime ExpiresUtc, string DisplayName);
public sealed record RoomJoin(string GameVersion);
public sealed record RoomLeave(Guid MatchId);
public sealed record TicketReply(Guid MatchId, Guid PlayerId, int Slot, string Ticket, DateTime ExpiresUtc,
    string Host, int Port, string TlsServerName);
public sealed record WorkerRegister(Guid InstanceId, string GameVersion);
public sealed record WorkerHeartbeat(Guid InstanceId);
public sealed record TicketRedeem(Guid InstanceId, string Ticket, string GameVersion, string ConnectionId);
public sealed record AdmissionReply(Guid MatchId, Guid PlayerId, int Slot);
public sealed record PlayerDisconnect(Guid InstanceId, Guid MatchId, Guid PlayerId, string ConnectionId);
public sealed record MatchResult(Guid InstanceId, Guid MatchId, int BlueScore, int RedScore);
public sealed record MatchAbandon(Guid InstanceId, Guid MatchId, string Reason);
public sealed record WorkerPlayer(Guid PlayerId, int Slot, bool Connected, string? ConnectionId);
public sealed record WorkerReply(Guid InstanceId, Guid? MatchId, string State, WorkerPlayer[] Players);
