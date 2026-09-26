namespace Clicalo.Application.Ports;

/// <summary>
/// Fixed chords that Clícalo itself must inject as internal effects, outside any shortcut (blueprint §3.6). In M2
/// the engine implements it through the ledger and <c>InjectionGate</c> (ADR-0004); in M1 the spikes use a guarded
/// test implementation (SpikeLab, Windowing.IntegrationTests). Every call sends a balanced batch (downs and ups
/// together) and never AltGr or right Ctrl.
/// </summary>
public interface IInternalKeyEffects
{
    /// <summary>
    /// Sends the reserved rights chord (Ctrl+Alt+Shift+F24). Callers check <see cref="IInternalRightsHotkey.IsRegistered"/>
    /// first; implementations refuse (return false) when the chord is not registered.
    /// </summary>
    /// <returns>True when the batch was injected.</returns>
    ValueTask<bool> SendRightsHotkeyAsync(CancellationToken cancellationToken);

    /// <summary>Sends Win+H, which toggles Windows dictation for the focused field (BUS-003, ACC-011).</summary>
    /// <returns>True when the batch was injected.</returns>
    ValueTask<bool> SendDictationChordAsync(CancellationToken cancellationToken);
}
