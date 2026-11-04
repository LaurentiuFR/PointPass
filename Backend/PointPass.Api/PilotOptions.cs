namespace PointPass.Api;

public sealed class PilotOptions
{
    public string WorkerKey { get; init; } = "";
    public string GameVersion { get; init; } = "PointPass-Online-1";
    public string GameHost { get; init; } = "62.238.115.207";
    public string TlsServerName { get; init; } = "62.238.115.207";
    public int GamePort { get; init; } = 7777;

    public void Validate()
    {
        if (WorkerKey.Length < 43 || WorkerKey.Length > 128)
            throw new InvalidOperationException("Configure a server-only PointPass:WorkerKey of at least 43 characters.");
        if (GameVersion.Length is < 1 or > 64 || GameHost.Length is < 1 or > 253 ||
            TlsServerName.Length is < 1 or > 253 || GamePort is < 1024 or > 65535)
            throw new InvalidOperationException("Invalid PointPass endpoint configuration.");
    }
}
