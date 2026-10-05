using Clicalo.App.Lifecycle;
using Clicalo.Platform.Core.Injection;

namespace Clicalo.App.Composition;

/// <summary>The release of a sending start: what <see cref="SystemKeyState"/> reports down, through the only <c>SendInput</c>.</summary>
/// <param name="sender">The product's <see cref="LowLevelInjector"/>.</param>
internal sealed class SystemPressedRelease(ILowLevelSender sender) : IPressedRelease
{
    /// <inheritdoc />
    public int ReleasePressed()
    {
        var outcome = PressedInputRelease.ReleaseOnce(SystemKeyState.Instance, sender);
        return outcome.Readable ? Math.Clamp(outcome.Send.Sent, 0, outcome.Events) : 0;
    }
}
