namespace Clicalo.Application.Foreground;

/// <summary>Why a <see cref="ForegroundLease"/> ended (blueprint §3.6, concurrency rules).</summary>
public enum LeaseEndReason
{
    /// <summary>Its owner ended it (<see cref="ForegroundLease.RestoreAsync"/> or disposal): the foreground was given back.</summary>
    Released,

    /// <summary>
    /// A newer lease took its place and inherited its previous foreground: nothing was restored in between, and a later
    /// <see cref="ForegroundLease.RestoreAsync"/> reports <see cref="RestoreOutcome.Failed"/> without touching the
    /// foreground, which now belongs to the newer lease.
    /// </summary>
    Replaced,

    /// <summary>
    /// The user switched apps (a verified external foreground change, new epoch). Nothing is restored: stealing the
    /// foreground back from the app the user chose would break REG-01.
    /// </summary>
    ForegroundChanged,

    /// <summary>No interaction for its idle timeout (<c>Timings.Foreground.TextInputLeaseIdle</c> for text input): restored.</summary>
    IdleTimeout,

    /// <summary>A terminal event of the engine (release all, exit...): restored.</summary>
    Terminal,
}
