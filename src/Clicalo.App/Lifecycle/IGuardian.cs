namespace Clicalo.App.Lifecycle;

/// <summary>
/// Starts and watches <c>Clicalo.Sentinel.exe</c> (blueprint §3.1, ADR-0022): the engine package's
/// <c>Platform.Windows/SentinelHost</c> launches it with exactly one inherited handle (the main process), watches it and
/// relaunches it with <c>Timings.Guardian.RestartBackoff</c>. The lifecycle only decides WHEN: in parallel to the first
/// frame (the default) or right after it (<c>--guardian after-first-frame</c>, spike S5).
/// </summary>
internal interface IGuardian
{
    /// <summary>Whether Sentinel is running.</summary>
    bool IsRunning { get; }

    /// <summary>
    /// Raised when Sentinel died <c>Timings.Guardian.RestartLoop</c> times and is no longer restarted: from then on
    /// nothing releases the keys if the process dies (D-22).
    /// </summary>
    event EventHandler? Unstable;

    /// <summary>Launches Sentinel; completes once it runs (or failed to start, which is logged and retried).</summary>
    /// <param name="cancellationToken">Stops the launch and the watching.</param>
    Task StartAsync(CancellationToken cancellationToken);
}
