using Clicalo.Application.Foreground;

namespace Clicalo.Tools.SpikeLab.Scripting;

/// <summary>What one foreground lease did, as the laboratory measured it (S4).</summary>
/// <param name="Kind">The lease kind requested.</param>
/// <param name="Origin">The origin requested.</param>
internal sealed record LeaseEvidence(LeaseKind Kind, LeaseOrigin Origin)
{
    /// <summary>True when <c>AcquireAsync</c> returned <c>Granted</c>.</summary>
    public bool Granted { get; init; }

    /// <summary>The reason of a denial.</summary>
    public ForegroundDenialReason? Denial { get; init; }

    /// <summary>
    /// A problem that prevented the lease from running at all (for example, a piece still pending in M1); null when
    /// the orchestrator answered.
    /// </summary>
    public string? Unavailable { get; init; }

    /// <summary>The step of the rights ladder: 2 when the laboratory injected the internal rights chord, else 1.</summary>
    public int LadderStep { get; init; } = 1;

    /// <summary>Milliseconds from the request to the answer of <c>AcquireAsync</c>.</summary>
    public double AcquireMs { get; init; }

    /// <summary>How the foreground was given back; null when the lease was not granted.</summary>
    public RestoreOutcome? Restore { get; init; }

    /// <summary>Milliseconds of <c>RestoreAsync</c>.</summary>
    public double RestoreMs { get; init; }

    /// <summary>The process that was in front before the request (process name only, never a title).</summary>
    public string? PreviousProcess { get; init; }

    /// <summary>True when InputProbe was in front before the request.</summary>
    public bool PreviousWasProbe { get; init; }

    /// <summary>True when, after the restoration, <c>GetForegroundWindow</c> is the window that was in front before.</summary>
    public bool ForegroundReturned { get; init; }
}
