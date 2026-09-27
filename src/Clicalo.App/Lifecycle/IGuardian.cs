namespace Clicalo.App.Lifecycle;

/// <summary>
/// Starts and watches <c>Clicalo.Sentinel.exe</c> (blueprint §3.1, ADR-0004): the engine package's
/// <c>Platform.Windows/SentinelHost</c> launches it with exactly three inherited handles, keeps the heartbeat pipe and
/// relaunches it with <c>Timings.Guardian.RestartBackoff</c>. The lifecycle only decides WHEN: in parallel to the first
/// frame (the default) or right after it (<c>--guardian after-first-frame</c>, spike S5).
/// </summary>
internal interface IGuardian
{
    /// <summary>Whether Sentinel is running and holds the ledger.</summary>
    bool IsRunning { get; }

    /// <summary>
    /// Raised when Sentinel died <c>Timings.Guardian.RestartLoop</c> times and is no longer restarted: from then on
    /// nothing releases the keys if the process dies, so the emergency never ends the process (D-22).
    /// </summary>
    event EventHandler? Unstable;

    /// <summary>Launches Sentinel; completes once it runs (or failed to start, which is logged and retried).</summary>
    /// <param name="cancellationToken">Stops the launch and the watching.</param>
    Task StartAsync(CancellationToken cancellationToken);
}
