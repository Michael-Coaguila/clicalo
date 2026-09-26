using Clicalo.Domain.Geometry;

namespace Clicalo.Application.Ports;

/// <summary>
/// Text entry without a physical keyboard for a field that already has the keyboard focus under a
/// <c>TextInput</c> lease (blueprint §3.6 step 2, BUS-002, BUS-003, REG-05). Implemented by
/// <c>Clicalo.Platform.Windows.Foreground.TouchKeyboard</c> with <c>IInputPaneInterop</c> and
/// <c>ITipInvocation</c>; dictation goes through <see cref="IInternalKeyEffects.SendDictationChordAsync"/>.
/// </summary>
public interface ITouchKeyboard
{
    /// <summary>
    /// The screen area the touch keyboard covers, in physical pixels; empty when hidden. The panel moves above it
    /// while the search is open (EC-BUS-01).
    /// </summary>
    PhysicalRect OccludedArea { get; }

    /// <summary>Raised when <see cref="OccludedArea"/> changes, on any thread.</summary>
    event EventHandler? OccludedAreaChanged;

    /// <summary>Shows the touch keyboard for the focused field of <paramref name="window"/>.</summary>
    /// <returns>True when the keyboard is visible afterwards.</returns>
    ValueTask<bool> ShowKeyboardAsync(WindowToken window, CancellationToken cancellationToken);

    /// <summary>Hides the touch keyboard if Clícalo showed it.</summary>
    ValueTask HideKeyboardAsync(CancellationToken cancellationToken);

    /// <summary>Starts Windows dictation (Win+H) for the focused field (BUS-003, ACC-011).</summary>
    /// <returns>True when the dictation chord was sent.</returns>
    ValueTask<bool> StartDictationAsync(CancellationToken cancellationToken);
}
