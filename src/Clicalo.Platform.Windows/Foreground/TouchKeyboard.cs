using System.Diagnostics.CodeAnalysis;
using Clicalo.Application.Ports;
using Clicalo.Domain.Geometry;

namespace Clicalo.Platform.Windows.Foreground;

/// <summary>
/// Adapter of <see cref="ITouchKeyboard"/> (blueprint §3.6 step 2): <c>IInputPaneInterop</c> for the occluded area
/// and <c>ITipInvocation::Toggle</c> to show the touch keyboard; Windows dictation (Win+H) through
/// <see cref="IInternalKeyEffects"/>, never through its own <c>SendInput</c>.
/// </summary>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality",
    Justification = "M1 contract stub: the foreground package implements it (docs/testing/spikes/M1-ownership.md)."
)]
public sealed class TouchKeyboard : ITouchKeyboard
{
    /// <summary>Creates the adapter; <paramref name="keyEffects"/> sends Win+H.</summary>
    public TouchKeyboard(IInternalKeyEffects keyEffects)
    {
        ArgumentNullException.ThrowIfNull(keyEffects);
        KeyEffects = keyEffects;
    }

    /// <inheritdoc />
    public event EventHandler? OccludedAreaChanged;

    /// <inheritdoc />
    public PhysicalRect OccludedArea => throw new NotImplementedException("M1 foreground package.");

    /// <summary>Sends the dictation chord.</summary>
    public IInternalKeyEffects KeyEffects { get; }

    /// <inheritdoc />
    public ValueTask<bool> ShowKeyboardAsync(
        WindowToken window,
        CancellationToken cancellationToken
    ) => throw new NotImplementedException("M1 foreground package.");

    /// <inheritdoc />
    public ValueTask HideKeyboardAsync(CancellationToken cancellationToken) =>
        throw new NotImplementedException("M1 foreground package.");

    /// <inheritdoc />
    public ValueTask<bool> StartDictationAsync(CancellationToken cancellationToken) =>
        KeyEffects.SendDictationChordAsync(cancellationToken);

    private void RaiseOccludedAreaChanged() => OccludedAreaChanged?.Invoke(this, EventArgs.Empty);
}
