using Clicalo.Application.Interaction;
using Clicalo.Domain.Dimming;

namespace Clicalo.Presentation.Dock;

/// <summary>
/// The opacity of every surface of the panel (GEN-009, docs/04 «Opacidad y atenuado»): the panel, the bar of the Tab
/// view and its handle, the windows beside it and the bubble. It tells the <see cref="InteractionStore"/> when a finger,
/// the pen or the pointer is on a surface and when one appears, and answers how much each kind of surface dims with
/// <see cref="InteractionStore.Dim"/> (<see cref="DimPolicy"/> with every exception of the state). It decides nothing.
/// </summary>
public sealed class SurfaceDimming
{
    private readonly InteractionStore _store;
    private DimSettings _settings;

    /// <summary>Creates the dimming of the surfaces of <paramref name="store"/>.</summary>
    /// <param name="store">The interaction state of the Surfaces role.</param>
    /// <param name="settings">The opacity and the dimming of General.</param>
    public SurfaceDimming(InteractionStore store, DimSettings settings)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
        _settings = settings;
        _store.Changed += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Raised when the decision of some surface may have changed: the state or the settings changed.</summary>
    public event EventHandler? Changed;

    /// <summary>The clock of the dimming.</summary>
    public TimeProvider Clock => _store.Clock;

    /// <summary>The opacity and the dimming of General, as last applied.</summary>
    public DimSettings Settings => _settings;

    /// <summary>Takes new opacity and dimming settings (GEN-009, AJR-004).</summary>
    /// <param name="settings">The settings.</param>
    public void ApplySettings(DimSettings settings)
    {
        if (settings == _settings)
        {
            return;
        }

        _settings = settings;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>How <paramref name="surface"/> dims now.</summary>
    /// <param name="surface">The kind of surface.</param>
    /// <param name="reduceMotion">Reduce motion is on (TEM-006).</param>
    /// <param name="highContrast">A contrast theme is on (PAN-003).</param>
    public DimDecision Decide(DimSurface surface, bool reduceMotion, bool highContrast) =>
        _store.Dim(surface, _settings, reduceMotion, highContrast);

    /// <summary>A finger, the pen or the pointer is on some surface of the panel (true), or on none (false).</summary>
    /// <param name="inside">Whether one is on a surface.</param>
    public void PointerPresence(bool inside)
    {
        if (inside != _store.Current.PointerInside)
        {
            _ = _store.Dispatch(
                inside
                    ? new InteractionAction.PointerEntered()
                    : new InteractionAction.PointerLeft()
            );
        }
    }

    /// <summary>A surface appeared: awake, it dims a while later unless a finger comes onto it.</summary>
    public void SurfaceShown() => _ = _store.Dispatch(new InteractionAction.SurfaceShown());
}
