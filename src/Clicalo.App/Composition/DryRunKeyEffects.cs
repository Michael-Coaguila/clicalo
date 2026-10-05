using Clicalo.Application.Ports;

namespace Clicalo.App.Composition;

/// <summary>
/// The internal key effects of <c>--no-input</c>: they never inject. The foreground ladder then skips its step 2 for
/// UI Automation origins, exactly as when another program owns the reserved chord (blueprint §3.6).
/// </summary>
internal sealed class DryRunKeyEffects : IInternalKeyEffects
{
    /// <inheritdoc />
    public ValueTask<bool> SendRightsHotkeyAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(false);

    /// <inheritdoc />
    public ValueTask<bool> SendDictationChordAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(false);
}
