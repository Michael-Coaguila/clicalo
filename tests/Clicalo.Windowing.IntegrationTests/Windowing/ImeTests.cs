using System.Globalization;
using Clicalo.TestKit.Windows;
using Clicalo.TestKit.Windows.Input;
using Clicalo.TestKit.Windows.Probe;
using Clicalo.Windowing.IntegrationTests.Desktop;
using Clicalo.Windowing.IntegrationTests.Windowing.Support;

namespace Clicalo.Windowing.IntegrationTests.Windowing;

/// <summary>
/// Spike S1, the necessary condition for an open IME composition to survive: while the panel is tapped, the
/// application in front receives no <c>WM_IME_ENDCOMPOSITION</c>, no <c>WM_IME_SETCONTEXT(FALSE)</c> (sent when it loses
/// the activation) and no <c>WM_KILLFOCUS</c>. The runner has no Japanese IME: the real composition is checked by hand
/// (S1.md, row 25).
/// </summary>
[Collection(DesktopCollectionDefinition.Name)]
[Trait("Requires", "Desktop")]
[Trait("Req", "REG-01")]
public sealed class ImeTests(SurfaceDesktopFixture desktop) : IClassFixture<SurfaceDesktopFixture>
{
    private const int Cycles = 20;

    [DesktopFact]
    public async Task A_tap_does_not_end_the_composition_of_the_foreground_window()
    {
        var panel = desktop.Panel;
        var cursor = await desktop.PrepareAsync();
        using var finger = desktop.CreatePointer(SyntheticPointerKind.Finger);

        for (var tap = 1; tap <= Cycles; tap++)
        {
            var landed = panel.PointerUps;
            var (x, y) = SurfaceDesktopFixture.CenterOf(panel);
            finger.Tap(x, y);
            await SurfaceDesktopFixture.WaitUntilAsync(
                () => panel.PointerUps > landed,
                Say($"Tap {tap} of {Cycles} did not reach the panel.")
            );
            ForegroundWindows
                .IsForeground(desktop.Probe.Window)
                .ShouldBeTrue(ForegroundWindows.Describe());
        }

        await desktop.ShouldHaveKeptTheForegroundAsync(cursor);
        var events = desktop.Probe.EventsSince(cursor);
        events
            .OfType<WindowMessageEvent>()
            .Where(message =>
                Is(message, "WM_IME_ENDCOMPOSITION")
                || (Is(message, "WM_IME_SETCONTEXT") && message.WParam == 0)
            )
            .Select(message => message.Json)
            .ShouldBeEmpty(
                "A tap on the panel would have ended the composition of the application in front."
            );
        events.OfType<FocusEvent>().Where(focus => !focus.IsGained).ShouldBeEmpty();
    }

    private static bool Is(WindowMessageEvent message, string name) =>
        string.Equals(message.MessageName, name, StringComparison.Ordinal);

    private static string Say(FormattableString text) =>
        text.ToString(CultureInfo.InvariantCulture);
}
