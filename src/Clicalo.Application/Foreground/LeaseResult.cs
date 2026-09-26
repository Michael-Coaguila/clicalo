namespace Clicalo.Application.Foreground;

/// <summary>The answer to <see cref="IForegroundOrchestrator.AcquireAsync"/> (blueprint §3.6).</summary>
public abstract record LeaseResult
{
    private LeaseResult() { }

    /// <summary>The lease was granted: the target is in the foreground now.</summary>
    /// <param name="Lease">The lease; dispose it to give the foreground back.</param>
    public sealed record Granted(ForegroundLease Lease) : LeaseResult;

    /// <summary>The lease was denied; nothing changed and the caller shows a live notice.</summary>
    /// <param name="Reason">Why.</param>
    public sealed record Denied(ForegroundDenialReason Reason) : LeaseResult;
}
