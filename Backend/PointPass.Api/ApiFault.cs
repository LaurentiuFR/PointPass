namespace PointPass.Api;

public sealed class ApiFault(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
    public static ApiFault Unauthorized() => new(401, "unauthorized", "Please initialize your guest session again.");
    public static ApiFault Invalid() => new(400, "invalid_request", "Check the supplied request fields.");
    public static ApiFault Version() => new(409, "incompatible_version", "Update PointPass to the pilot version.");
    public static ApiFault Unavailable() => new(503, "worker_unavailable", "The test server is temporarily unavailable.");
}
