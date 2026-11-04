namespace PointPass.Api;

public sealed class SecurityCounters
{
    private long malformed, unauthorized, forbidden, conflict, limited, unavailable;

    public void Reject(int status)
    {
        switch (status)
        {
            case 400:
            case 404:
            case 405:
            case 413: Interlocked.Increment(ref malformed); break;
            case 401: Interlocked.Increment(ref unauthorized); break;
            case 403: Interlocked.Increment(ref forbidden); break;
            case 409: Interlocked.Increment(ref conflict); break;
            case 429: Interlocked.Increment(ref limited); break;
            default: if (status >= 500) Interlocked.Increment(ref unavailable); break;
        }
    }

    public object Snapshot() => new
    {
        malformed = Interlocked.Read(ref malformed), unauthorized = Interlocked.Read(ref unauthorized),
        forbidden = Interlocked.Read(ref forbidden), conflict = Interlocked.Read(ref conflict),
        limited = Interlocked.Read(ref limited), unavailable = Interlocked.Read(ref unavailable)
    };
}
