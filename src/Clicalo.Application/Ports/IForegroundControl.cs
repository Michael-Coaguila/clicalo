namespace Clicalo.Application.Ports;

/// <summary>
/// The only way to change the foreground window (blueprint §3.6, ADR-0005). Implemented once, by
/// <c>Clicalo.Platform.Windows.Foreground.ForegroundControl</c> (the only file allowed to call
/// <c>SetForegroundWindow</c>); used only by <c>Clicalo.Application.Foreground</c> (ArchUnit rule). Every method is
/// synchronous, never blocks and may be called from any thread.
/// </summary>
public interface IForegroundControl
{
    /// <summary>
    /// Asks Windows to bring <paramref name="window"/> to the foreground once (<c>SetForegroundWindow</c>) and
    /// reports whether <c>GetForegroundWindow</c> is that window afterwards. Retries, verification delays and the
    /// rights ladder belong to the orchestrator, not to this call.
    /// </summary>
    bool TrySetForeground(WindowToken window);

    /// <summary>The current foreground window (<c>GetForegroundWindow</c>); <see cref="WindowToken.None"/> if none.</summary>
    WindowToken GetForeground();

    /// <summary>Flashes the taskbar button of <paramref name="window"/> until it is activated (PRB-007).</summary>
    void FlashTaskbar(WindowToken window);
}
