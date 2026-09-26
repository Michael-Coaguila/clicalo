using System.Globalization;
using Clicalo.Application.Foreground;
using Clicalo.Application.Ports;
using Clicalo.Domain.Geometry;
using Clicalo.Platform.IntegrationTests.Desktop;
using Clicalo.Platform.Windows.Tray;
using Clicalo.TestKit.Windows;

namespace Clicalo.Platform.IntegrationTests.Foreground;

/// <summary>
/// <c>ForegroundOrchestrator</c> end to end over the real adapters (spike S4), with InputProbe as the app in front: a
/// text input lease brings a test window of this process to the foreground and gives the foreground back to the
/// probe, verified, 20 of 20; the UI Automation origin climbs to the internal rights hotkey and the probe never
/// receives its F24 press; and the tray menu lease returns to the probe after the menu closes.
/// </summary>
/// <remarks>
/// <para>
/// The test window stands in for the search surface: the surfaces themselves (<c>NonActivatingWindow</c>,
/// <c>SurfaceRegistry</c>) belong to the windowing package and are exercised by the Windowing integration tests.
/// Where a real origin needs input this process cannot legitimately produce (a finger on the surface, the shell's
/// tray click), the right comes from the reserved chord injected into the probe: the same <c>WM_HOTKEY</c> mechanism
/// as the global shortcut. Safety: see <see cref="GuardedInternalKeyEffects"/>.
/// </para>
/// <para>
/// This process always holds the foreground right, because Windows gives it to the process that injected the last
/// input (S4 finding). To run step 2 of the ladder for real, the UI Automation test refuses the first attempt of each
/// cycle with <see cref="FirstAttemptRefusingControl"/>; everything after it (arming, the guarded chord,
/// <c>WM_HOTKEY</c>, the verified retry) is the real thing.
/// </para>
/// </remarks>
[Collection(DesktopCollectionDefinition.Name)]
[Trait("Requires", "Desktop")]
[Trait("Req", "REG-01")]
public sealed class ForegroundLeaseCycleTests : IClassFixture<ForegroundDesktopFixture>
{
    private const int Cycles = 20;

    private static readonly TrayMenuItem[] MenuItems = [new(1, "1"), new(2, "2")];

    private readonly DesktopProbeFixture _desktop;
    private readonly ForegroundDesktopFixture _foreground;

    public ForegroundLeaseCycleTests(
        DesktopProbeFixture desktop,
        ForegroundDesktopFixture foreground
    )
    {
        _desktop = desktop;
        _foreground = foreground;
        if (DesktopTestEnvironment.IsEnabled)
        {
            foreground.ProbeWindow = desktop.Probe.Window;
        }
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private WindowToken Probe => new(_desktop.Probe.Window);

    [DesktopFact]
    [Trait("Req", "BUS-002")]
    public async Task A_text_input_lease_on_a_test_window_gives_the_foreground_back_to_the_probe_20_of_20()
    {
        using var orchestrator = _foreground.CreateOrchestrator();
        var steps = new List<LadderStep>();
        var outcomes = new List<RestoreOutcome>();

        for (var cycle = 0; cycle < Cycles; cycle++)
        {
            await PrepareCycleAsync();
            await _foreground.GainRightsAsync(Cancellation);
            _foreground.Window.IsNonActivating.ShouldBeTrue();

            var result = await orchestrator.AcquireAsync(
                new LeaseRequest(
                    LeaseKind.TextInput,
                    _foreground.Window.Token,
                    LeaseOrigin.GlobalHotkey,
                    null
                ),
                Cancellation
            );

            var lease = result.ShouldBeOfType<LeaseResult.Granted>(Describe(cycle)).Lease;
            steps.Add(lease.GrantedAt);
            lease.PreviousForeground.ShouldBe(Probe, Describe(cycle));
            ForegroundWindows.IsForeground(_foreground.Window.Handle).ShouldBeTrue(Describe(cycle));
            _foreground.Window.IsNonActivating.ShouldBeFalse(
                "activation is allowed while the lease lasts"
            );
            orchestrator.IsActivationLeased(_foreground.Window.Token).ShouldBeTrue();

            var outcome = await lease.RestoreAsync(Cancellation);

            outcomes.Add(outcome);
            outcome.ShouldBeOneOf(RestoreOutcome.Restored, RestoreOutcome.RestoredAfterRetry);
            _desktop.Probe.IsForeground.ShouldBeTrue(Describe(cycle));
            _foreground.Window.IsNonActivating.ShouldBeTrue("WS_EX_NOACTIVATE goes back");
            orchestrator.IsActivationLeased(_foreground.Window.Token).ShouldBeFalse();
        }

        outcomes.Count.ShouldBe(Cycles);
        Report("Ladder steps", steps);
        Report("Restore outcomes", outcomes);
    }

    [DesktopFact]
    [Trait("Req", "BUS-002")]
    public async Task The_Uia_origin_climbs_to_the_internal_hotkey_and_the_probe_never_receives_its_F24_press_20_of_20()
    {
        _foreground.RequireRegisteredHotkey();
        var control = new FirstAttemptRefusingControl(_foreground.Control);
        using var orchestrator = _foreground.CreateOrchestrator(control);
        var steps = new List<LadderStep>();
        var chords = _foreground.Keys.RightsChords;
        var cursor = await _desktop.PrepareAsync();

        for (var cycle = 0; cycle < Cycles; cycle++)
        {
            await PrepareCycleAsync();
            control.RefuseNextAttempt();

            var result = await orchestrator.AcquireAsync(
                new LeaseRequest(
                    LeaseKind.TextInput,
                    _foreground.Window.Token,
                    LeaseOrigin.UiaInvoke,
                    null
                ),
                Cancellation
            );

            var lease = result.ShouldBeOfType<LeaseResult.Granted>(Describe(cycle)).Lease;
            steps.Add(lease.GrantedAt);
            ForegroundWindows.IsForeground(_foreground.Window.Handle).ShouldBeTrue(Describe(cycle));
            (await lease.RestoreAsync(Cancellation)).ShouldBeOneOf(
                RestoreOutcome.Restored,
                RestoreOutcome.RestoredAfterRetry
            );
            _desktop.Probe.IsForeground.ShouldBeTrue(Describe(cycle));
        }

        var events = await _desktop.Probe.CollectAsync(
            cursor,
            _ => true,
            DesktopProbeFixture.EventTimeout,
            Cancellation
        );
        steps.ShouldAllBe(step => step == LadderStep.RightsHotkey);
        (_foreground.Keys.RightsChords - chords).ShouldBe(Cycles);
        ProbeInput.ReservedKeyPresses(events).ShouldBeEmpty("no F24 press reaches an app (S4)");
        ProbeEvents.TypedChars(events).ShouldBeEmpty("no character reaches an app (S4)");
        ProbeInput.KeyMenus(events).ShouldBeEmpty("no menu opens in the app (S4)");
        TestContext.Current.TestOutputHelper?.WriteLine(
            "Chord messages seen by the probe: "
                + ProbeInput.Summary(
                    ProbeInput.ChordModifiers(events).Concat(ProbeInput.ReservedKeyReleases(events))
                )
        );
    }

    [DesktopFact]
    [Trait("Req", "BUR-003")]
    public async Task The_tray_menu_lease_gives_the_foreground_back_to_the_probe_after_the_menu_closes()
    {
        using var orchestrator = _foreground.CreateOrchestrator();
        var host = _foreground.Tray;
        var cursor = await _desktop.PrepareAsync();

        for (var cycle = 0; cycle < Cycles; cycle++)
        {
            await PrepareCycleAsync();
            await _foreground.GainRightsAsync(Cancellation);

            var result = await orchestrator.AcquireAsync(
                new LeaseRequest(LeaseKind.TrayMenu, host.Window, LeaseOrigin.Tray, null),
                Cancellation
            );
            var lease = result.ShouldBeOfType<LeaseResult.Granted>(Describe(cycle)).Lease;
            lease.PreviousForeground.ShouldBe(Probe, Describe(cycle));
            ForegroundWindows.IsForeground(host.Window.Handle).ShouldBeTrue(Describe(cycle));

            var opened = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously
            );
            EventHandler onOpened = (_, _) => opened.TrySetResult();
            host.MenuOpened += onOpened;
            var menu = host.ShowMenuAsync(MenuItems, new PhysicalPoint(200, 200));
            bool wasOpen;
            try
            {
                await opened.Task.WaitAsync(DesktopProbeFixture.EventTimeout, Cancellation);
                wasOpen = host.IsMenuOpen;
            }
            finally
            {
                // Whatever happened, the menu never stays on the screen.
                host.MenuOpened -= onOpened;
                host.DismissMenu();
            }

            var chosen = await menu.WaitAsync(DesktopProbeFixture.EventTimeout, Cancellation);
            wasOpen.ShouldBeTrue(Describe(cycle));
            chosen.ShouldBeNull("the menu was dismissed");
            var outcome = await lease.RestoreAsync(Cancellation);
            outcome.ShouldBeOneOf(RestoreOutcome.Restored, RestoreOutcome.RestoredAfterRetry);
            _desktop.Probe.IsForeground.ShouldBeTrue(Describe(cycle));
        }

        var events = await _desktop.Probe.CollectAsync(
            cursor,
            _ => true,
            DesktopProbeFixture.EventTimeout,
            Cancellation
        );
        ProbeInput.ReservedKeyPresses(events).ShouldBeEmpty();
        ProbeEvents.TypedChars(events).ShouldBeEmpty();
    }

    private static string Describe(int cycle) =>
        string.Create(CultureInfo.InvariantCulture, $"cycle {cycle + 1} of {Cycles}: ")
        + ForegroundWindows.Describe();

    private static void Report<T>(string title, IEnumerable<T> values) =>
        TestContext.Current.TestOutputHelper?.WriteLine(
            title
                + ": "
                + string.Join(
                    ", ",
                    values
                        .GroupBy(value => value)
                        .Select(group =>
                            string.Create(
                                CultureInfo.InvariantCulture,
                                $"{group.Key} × {group.Count()}"
                            )
                        )
                )
        );

    /// <summary>
    /// The probe in front, and confirmed as such by the monitor: the lease returns to the external foreground the
    /// monitor confirmed last, and its WinEvents arrive asynchronously.
    /// </summary>
    private async Task PrepareCycleAsync()
    {
        _ = await _desktop.PrepareAsync();
        await _foreground.WaitUntilMonitorSeesAsync(_desktop.Probe.Window, Cancellation);
    }
}
