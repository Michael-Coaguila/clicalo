using System.Globalization;
using Clicalo.Application.Foreground;
using Clicalo.TestKit.Windows;
using Clicalo.TestKit.Windows.Input;
using Clicalo.TestKit.Windows.Probe;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.Windowing.IntegrationTests.Desktop;
using Clicalo.Windowing.IntegrationTests.Foreground.Support;
using Clicalo.Windowing.IntegrationTests.Windowing.Support;

namespace Clicalo.Windowing.IntegrationTests.Foreground;

/// <summary>
/// Spike S4 (docs/testing/spikes/S4.md) with the real surfaces: the search (<c>NonActivatingWindow</c> with a text
/// field) opened under a <c>TextInput</c> lease by the real <c>ForegroundOrchestrator</c>, with InputProbe as the app in
/// front. Per origin, 20 cycles: the search comes to the front, its field has the keyboard focus and it has no
/// <c>WS_EX_NOACTIVATE</c> while the lease lasts; closing it gives the foreground back to the probe, verified, and puts
/// <c>WS_EX_NOACTIVATE</c> back. A denied lease changes nothing and says why.
/// </summary>
/// <remarks>
/// This test process always holds the foreground right: Windows gives it to the process that injected the last input
/// (S4, finding 2), and the synthetic finger and the chord are injected from here. So the touch and global shortcut
/// origins succeed at step 1 here as they do in the product, where the right comes from the input Clícalo received,
/// and the denial is produced by <see cref="RefusingForegroundControl"/>, as Windows refuses a process without the right.
/// </remarks>
[Collection(DesktopCollectionDefinition.Name)]
[Trait("Requires", "Desktop")]
[Trait("Req", "BUS-002")]
[Trait("Req", "REG-01")]
public sealed class TextInputLeaseTests(LeaseDesktopFixture desktop)
    : IClassFixture<LeaseDesktopFixture>
{
    private const int Cycles = 20;

    /// <summary><c>VK_F24</c>, the key of the reserved chord that stands in for the global shortcut.</summary>
    private const VirtualKeyCode F24 = (VirtualKeyCode)0x87;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [DesktopFact]
    [Trait("Req", "REG-05")]
    public async Task Touch_origin_opens_the_search_and_gives_the_foreground_back()
    {
        var violations = desktop.Lab.Guard.Violations;
        var cursor = await desktop.PrepareAsync();
        var steps = new List<LadderStep>();
        var outcomes = new List<RestoreOutcome>();

        for (var cycle = 1; cycle <= Cycles; cycle++)
        {
            await desktop.PrepareAsync();

            var result = await desktop.TapPanelAsync(() =>
                desktop.OpenSearchAsync(LeaseOrigin.Touch)
            );

            var lease = await ShouldHaveOpenedTheSearchAsync(result, cycle);
            steps.Add(lease.GrantedAt);
            outcomes.Add(await CloseAndShouldHaveGivenBackAsync(lease, cycle));
        }

        desktop.Lab.Guard.Violations.ShouldBe(
            violations,
            "an activation under the lease is not a violation"
        );
        await desktop.Probe.PingAsync(LeaseDesktopFixture.EventTimeout, Cancellation);
        desktop
            .Probe.EventsSince(cursor)
            .OfType<CharMessageEvent>()
            .Where(message => message.IsTyped)
            .ShouldBeEmpty("a touch types nothing into the app");
        Report("Ladder steps", steps);
        Report("Restore outcomes", outcomes);
    }

    [DesktopFact]
    public async Task Global_hotkey_origin_opens_the_search()
    {
        desktop.RequireRegisteredHotkey();
        var violations = desktop.Lab.Guard.Violations;
        var cursor = await desktop.PrepareAsync();
        var steps = new List<LadderStep>();
        var outcomes = new List<RestoreOutcome>();

        for (var cycle = 1; cycle <= Cycles; cycle++)
        {
            await desktop.PrepareAsync();
            await PressTheGlobalShortcutAsync(cycle);

            var result = await LeaseDesktopFixture.OnUiThreadAsync(() =>
                desktop.OpenSearchAsync(LeaseOrigin.GlobalHotkey)
            );

            var lease = await ShouldHaveOpenedTheSearchAsync(result, cycle);
            steps.Add(lease.GrantedAt);
            outcomes.Add(await CloseAndShouldHaveGivenBackAsync(lease, cycle));
        }

        desktop.Lab.Guard.Violations.ShouldBe(violations);
        await desktop.Probe.PingAsync(LeaseDesktopFixture.EventTimeout, Cancellation);
        var events = desktop.Probe.EventsSince(cursor);
        var injected = events
            .OfType<KeyMessageEvent>()
            .Where(key => key.ExtraInfo == TestKeyboardInjector.ExtraInfoMarker)
            .ToList();
        injected
            .Where(key => key.VirtualKey == F24 && key.IsPress)
            .ShouldBeEmpty(
                "the system consumes the registered chord: no F24 press reaches the app"
            );
        events
            .OfType<CharMessageEvent>()
            .Where(message => message.IsTyped)
            .ShouldBeEmpty("the chord types nothing into the app");
        foreach (var key in injected.Where(IsModifier).GroupBy(key => key.SideVirtualKey))
        {
            key.Count(message => message.IsRelease)
                .ShouldBe(
                    key.Count(message => message.IsPress),
                    $"every {key.Key} the app received pressed is released in the app too, or it stays down there"
                );
        }

        Report("Ladder steps", steps);
        Report("Restore outcomes", outcomes);
    }

    [DesktopFact]
    public async Task A_denied_lease_sends_nothing_and_says_why()
    {
        var control = new RefusingForegroundControl(desktop.Control);
        using var refusing = desktop.CreateOrchestrator(control);
        var violations = desktop.Lab.Guard.Violations;
        var rights = desktop.Keys.RightsRequests;
        var search = desktop.Search;
        var cursor = await desktop.PrepareAsync();

        for (var cycle = 1; cycle <= Cycles; cycle++)
        {
            await LeaseDesktopFixture.WaitUntilAsync(
                () => refusing.Current.Window == desktop.ProbeWindow,
                Say($"Cycle {cycle}: the orchestrator never verified InputProbe.")
            );

            var result = await LeaseDesktopFixture.OnUiThreadAsync(() =>
                desktop.OpenSearchAsync(LeaseOrigin.Internal, refusing)
            );

            result
                .ShouldBeOfType<LeaseResult.Denied>(Say($"Cycle {cycle}"))
                .Reason.ShouldBe(ForegroundDenialReason.RightsRefused, "it says why");
            desktop.Probe.IsForeground.ShouldBeTrue(
                Say($"Cycle {cycle}: the foreground changed: {ForegroundWindows.Describe()}")
            );
            NativeSurface
                .HasExStyle(search.Handle, NativeSurface.ExNoActivate)
                .ShouldBeTrue(Say($"Cycle {cycle}: WS_EX_NOACTIVATE is back after the denial."));
            desktop.Lab.Registry.IsActivationAllowed(search.Id).ShouldBeFalse();
            refusing.IsActivationLeased(search.SurfaceWindow).ShouldBeFalse();
            refusing.ActiveLease.ShouldBeNull();
        }

        control.Attempts.ShouldBe(
            Cycles,
            "an internal origin has step 1 only: one attempt per request"
        );
        control.Flashes.ShouldBe(0);
        desktop.Keys.RightsRequests.ShouldBe(rights, "no chord is sent for an internal origin");
        desktop.Lab.Guard.Violations.ShouldBe(violations);
        WpfThread.Invoke(() => search.IsVisible).ShouldBeFalse("the denied search is hidden again");
        await ShouldHaveKeptTheForegroundAsync(cursor);
    }

    private static bool IsModifier(KeyMessageEvent key) =>
        key.VirtualKey is VirtualKeyCode.Control or VirtualKeyCode.Menu or VirtualKeyCode.Shift;

    private static string Say(FormattableString text) =>
        text.ToString(CultureInfo.InvariantCulture);

    private static void Report<T>(string title, IEnumerable<T> values) =>
        TestContext.Current.TestOutputHelper?.WriteLine(
            title
                + ": "
                + string.Join(
                    ", ",
                    values
                        .GroupBy(value => value?.ToString() ?? string.Empty, StringComparer.Ordinal)
                        .Select(group => group.Key + " × " + group.Count())
                )
        );

    /// <summary>
    /// The global shortcut pressed with the probe in front: the reserved chord (left modifiers only) injected into the
    /// probe, checked immediately before the batch, with its releases in the same batch; then its <c>WM_HOTKEY</c>
    /// and the release of its keys, which must reach the probe before the foreground moves.
    /// </summary>
    private async Task PressTheGlobalShortcutAsync(int cycle)
    {
        var arrived = desktop.Hotkey.WaitForRightsAsync(Cancellation).AsTask();
        new TestKeyboardInjector(desktop.Probe).Send(
            KeyStrokes.Chord(
                VirtualKeyCode.LeftControl,
                VirtualKeyCode.LeftMenu,
                VirtualKeyCode.LeftShift,
                F24
            )
        );
        (await arrived).ShouldBeTrue(Say($"Cycle {cycle}: WM_HOTKEY did not arrive in time."));
        (await desktop.Hotkey.WaitForChordReleaseAsync(Cancellation)).ShouldBeTrue(
            Say($"Cycle {cycle}: a key of the chord stayed down.")
        );
    }

    private async Task<ForegroundLease> ShouldHaveOpenedTheSearchAsync(
        LeaseResult result,
        int cycle
    )
    {
        var search = desktop.Search;
        var lease = result.ShouldBeOfType<LeaseResult.Granted>(Say($"Cycle {cycle}")).Lease;
        lease.PreviousForeground.ShouldBe(desktop.ProbeWindow, Say($"Cycle {cycle}"));
        ForegroundWindows
            .IsForeground(search.Handle)
            .ShouldBeTrue(
                Say($"Cycle {cycle}: the search is not in front: {ForegroundWindows.Describe()}")
            );
        await LeaseDesktopFixture.WaitUntilAsync(
            () => WpfThread.Invoke(() => search.IsActive && search.FieldHasKeyboardFocus),
            Say($"Cycle {cycle}: the search field does not have the keyboard focus.")
        );
        NativeSurface
            .HasExStyle(search.Handle, NativeSurface.ExNoActivate)
            .ShouldBeFalse(Say($"Cycle {cycle}: WS_EX_NOACTIVATE stayed during the lease."));
        desktop.Orchestrator.IsActivationLeased(search.SurfaceWindow).ShouldBeTrue();
        return lease;
    }

    private async Task<RestoreOutcome> CloseAndShouldHaveGivenBackAsync(
        ForegroundLease lease,
        int cycle
    )
    {
        var search = desktop.Search;
        var outcome = await LeaseDesktopFixture.OnUiThreadAsync(() =>
            desktop.CloseSearchAsync(lease)
        );

        outcome.ShouldBeOneOf(RestoreOutcome.Restored, RestoreOutcome.RestoredAfterRetry);
        ForegroundWindows
            .IsForeground(desktop.Probe.Window)
            .ShouldBeTrue(
                Say(
                    $"Cycle {cycle}: the probe is not back in front: {ForegroundWindows.Describe()}"
                )
            );
        NativeSurface
            .HasExStyle(search.Handle, NativeSurface.ExNoActivate)
            .ShouldBeTrue(Say($"Cycle {cycle}: WS_EX_NOACTIVATE did not come back."));
        desktop.Orchestrator.IsActivationLeased(search.SurfaceWindow).ShouldBeFalse();
        return outcome;
    }

    /// <summary>
    /// The probe kept the foreground and the keyboard focus since <paramref name="cursor"/> and received no key.
    /// </summary>
    private async Task ShouldHaveKeptTheForegroundAsync(int cursor)
    {
        await desktop.Probe.PingAsync(LeaseDesktopFixture.EventTimeout, Cancellation);
        var events = desktop.Probe.EventsSince(cursor);
        events
            .OfType<ActivateEvent>()
            .Where(activate => activate.State == ActivationState.Inactive)
            .ShouldBeEmpty("The probe was deactivated.");
        events
            .OfType<AppActivateEvent>()
            .Where(activate => !activate.IsActive)
            .ShouldBeEmpty("The probe's application was deactivated.");
        events
            .OfType<FocusEvent>()
            .Where(focus => !focus.IsGained)
            .ShouldBeEmpty("The probe lost the keyboard focus.");
        events.OfType<KeyMessageEvent>().ShouldBeEmpty("Nothing was sent to the probe.");
    }
}
