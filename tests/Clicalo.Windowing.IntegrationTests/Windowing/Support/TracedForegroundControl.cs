using System.Globalization;
using Clicalo.Application.Ports;
using Clicalo.TestKit.Windows;

namespace Clicalo.Windowing.IntegrationTests.Windowing.Support;

/// <summary>
/// The real <see cref="IForegroundControl"/> with every call of the orchestrator noted on an
/// <see cref="ActivationTimeline"/>: what it asked, what it saw and the foreground window around it.
/// </summary>
public sealed class TracedForegroundControl(IForegroundControl inner, ActivationTimeline timeline)
    : IForegroundControl
{
    public bool TrySetForeground(WindowToken window)
    {
        timeline.Note(
            Say(
                $"orchestrator SetForegroundWindow(0x{window.Handle:X}), foreground 0x{ForegroundWindows.Current:X}"
            )
        );
        var verified = inner.TrySetForeground(window);
        timeline.Note(
            Say(
                $"orchestrator SetForegroundWindow(0x{window.Handle:X}) verified {verified}, foreground 0x{ForegroundWindows.Current:X}"
            )
        );
        return verified;
    }

    public WindowToken GetForeground()
    {
        var foreground = inner.GetForeground();
        timeline.Note(Say($"orchestrator GetForegroundWindow 0x{foreground.Handle:X}"));
        return foreground;
    }

    public void FlashTaskbar(WindowToken window)
    {
        timeline.Note(Say($"orchestrator FlashWindowEx(0x{window.Handle:X})"));
        inner.FlashTaskbar(window);
    }

    private static string Say(FormattableString text) =>
        text.ToString(CultureInfo.InvariantCulture);
}
