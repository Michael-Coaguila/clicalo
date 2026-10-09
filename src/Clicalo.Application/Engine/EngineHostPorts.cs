using Clicalo.Application.Ports;

namespace Clicalo.Application.Engine;

/// <summary>The ports the engine host interprets effects against (blueprint §7.3).</summary>
/// <param name="Injector">Sends input.</param>
/// <param name="Shell">Launches and system commands on the Shell thread.</param>
/// <param name="Clipboard">Pastes on the SysEvents thread.</param>
/// <param name="Observer">Receives snapshots, notices and usage.</param>
public sealed record EngineHostPorts(
    IInputInjector Injector,
    IShellExecutor Shell,
    IClipboardPaster Clipboard,
    IEngineObserver Observer
)
{
    /// <summary>
    /// Where the host answers the internal chords it sent (<see cref="EngineKeyEffects"/>, blueprint §3.6); when
    /// absent, nobody waits for them.
    /// </summary>
    public InternalChordReplies? ChordReplies { get; init; }

    /// <summary>
    /// The last pointer position outside Clícalo, given to every activation that does not bring one (EJE-009); when
    /// absent, mouse actions act at the centre of the foreground window.
    /// </summary>
    public IPointerPositionSource? PointerPosition { get; init; }

    /// <summary>The soft sound after an action (EJE-012); when absent, nothing plays.</summary>
    public IFeedbackSound? Sound { get; init; }
}
