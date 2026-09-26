using System.Globalization;
using System.Windows.Input;
using Clicalo.Application.Foreground;
using Clicalo.TestKit.Windows;
using Clicalo.TestKit.Windows.Input;
using Clicalo.TestKit.Windows.Probe;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.Windowing.IntegrationTests.Desktop;
using Clicalo.Windowing.IntegrationTests.Foreground.Support;

namespace Clicalo.Windowing.IntegrationTests.Foreground;

/// <summary>
/// Spike S4, BUS-003 and ACC-011: Windows dictation (Win+H) through the real <c>TouchKeyboard</c> is sent only to a field
/// of Clícalo that has the keyboard focus. With InputProbe in front it is refused without injecting anything; with the
/// search open under its lease and its field focused it passes every check of the guard; with the search in front but
/// its field unfocused it is refused. 20 cycles.
/// </summary>
/// <remarks>
/// The last step of the checked batch is recorded instead of sent (<see cref="GuardedDictationKeys"/>): a real Win+H
/// opens voice typing with the microphone. The real chord is part of the manual rows of S4.
/// </remarks>
[Collection(DesktopCollectionDefinition.Name)]
[Trait("Requires", "Desktop")]
[Trait("Req", "BUS-003")]
[Trait("Req", "ACC-011")]
[Trait("Req", "REG-01")]
public sealed class DictationTests(LeaseDesktopFixture desktop) : IClassFixture<LeaseDesktopFixture>
{
    private const int Cycles = 20;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [DesktopFact]
    public async Task Win_H_is_sent_only_to_an_own_focused_field()
    {
        var search = desktop.Search;
        var cursor = await desktop.PrepareAsync();
        var sent = desktop.Keys.Sent.Count;
        var refused = desktop.Keys.Refused;

        for (var cycle = 1; cycle <= Cycles; cycle++)
        {
            await desktop.PrepareAsync();

            // Another app in front: nothing is injected.
            (await desktop.Keyboard.StartDictationAsync(Cancellation)).ShouldBeFalse(
                Say($"Cycle {cycle}: Win+H with the probe in front.")
            );
            desktop.Keys.Sent.Count.ShouldBe(sent, Say($"Cycle {cycle}"));

            // The search open under its lease, its field focused: Win+H goes to it.
            var result = await desktop.TapPanelAsync(() =>
                desktop.OpenSearchAsync(LeaseOrigin.Touch)
            );
            var lease = result.ShouldBeOfType<LeaseResult.Granted>(Say($"Cycle {cycle}")).Lease;
            await LeaseDesktopFixture.WaitUntilAsync(
                () => WpfThread.Invoke(() => search.IsActive && search.FieldHasKeyboardFocus),
                Say($"Cycle {cycle}: the search field does not have the keyboard focus.")
            );
            (await desktop.Keyboard.StartDictationAsync(Cancellation)).ShouldBeTrue(
                Say(
                    $"Cycle {cycle}: Win+H refused for the focused search field: {ForegroundWindows.Describe()}"
                )
            );
            sent++;
            var (target, batch) = desktop.Keys.Sent[^1];
            target.ShouldBe(search.Handle);
            batch
                .Select(stroke => (stroke.VirtualKey, stroke.IsKeyUp))
                .ShouldBe(
                    [
                        (VirtualKeyCode.LeftWindows, false),
                        (VirtualKeyCode.H, false),
                        (VirtualKeyCode.H, true),
                        (VirtualKeyCode.LeftWindows, true),
                    ],
                    "left Windows and H, released in the same batch"
                );

            // The search in front, but its field without the keyboard focus: refused.
            WpfThread.Invoke(Keyboard.ClearFocus);
            (await desktop.Keyboard.StartDictationAsync(Cancellation)).ShouldBeFalse(
                Say($"Cycle {cycle}: Win+H with no field focused.")
            );
            desktop.Keys.Sent.Count.ShouldBe(sent, Say($"Cycle {cycle}"));

            (
                await LeaseDesktopFixture.OnUiThreadAsync(() => desktop.CloseSearchAsync(lease))
            ).ShouldBeOneOf(RestoreOutcome.Restored, RestoreOutcome.RestoredAfterRetry);
            ForegroundWindows
                .IsForeground(desktop.Probe.Window)
                .ShouldBeTrue(Say($"Cycle {cycle}"));
        }

        desktop.Keys.Refused.ShouldBe(refused + (2 * Cycles));
        await desktop.Probe.PingAsync(LeaseDesktopFixture.EventTimeout, Cancellation);
        desktop
            .Probe.EventsSince(cursor)
            .OfType<KeyMessageEvent>()
            .Where(key => key.ExtraInfo == TestKeyboardInjector.ExtraInfoMarker)
            .ShouldBeEmpty("no key reached the probe");
    }

    private static string Say(FormattableString text) =>
        text.ToString(CultureInfo.InvariantCulture);
}
