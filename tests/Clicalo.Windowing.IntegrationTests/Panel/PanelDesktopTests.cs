using System.Globalization;
using Clicalo.Application.Engine;
using Clicalo.Application.Session;
using Clicalo.Domain.Execution;
using Clicalo.Domain.KeySafety;
using Clicalo.Domain.Primitives;
using Clicalo.Presentation.Panel;
using Clicalo.TestKit.Windows;
using Clicalo.TestKit.Windows.Input;
using Clicalo.TestKit.Windows.Probe;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.Windowing.IntegrationTests.Desktop;
using FlaUI.UIA3;

namespace Clicalo.Windowing.IntegrationTests.MinimalPanel;

/// <summary>
/// The real M2 panel on a real desktop with InputProbe in front (blueprint §14, M2 exit criteria): finger, pen and mouse
/// taps and holds reach the engine as the single activation flow expects, UI Automation invokes a tile, the panic
/// strip releases everything, and none of it ever takes the foreground or the keyboard focus from the app
/// (<c>reg01.violations = 0</c>, REG-01). The engine is a recorder: no key is sent anywhere.
/// </summary>
[Trait("Requires", "Desktop")]
[Collection(DesktopCollectionDefinition.Name)]
[Trait("Req", "REG-01")]
public sealed class PanelDesktopTests(PanelDesktopFixture fixture)
    : IClassFixture<PanelDesktopFixture>
{
    /// <summary>Taps of the non-activation cycle (20 per M2 criterion, spread over finger, pen and mouse).</summary>
    private const int Taps = 21;

    /// <summary>
    /// p95 of «Windows records the lift → the activation is in the engine mailbox»: the panel's share of the 50 ms budget
    /// of NFR-001 (the whole «touch → SendInput» is measured end to end by Clicalo.Performance in the CI).
    /// </summary>
    private static readonly TimeSpan PanelLatencyBudget = TimeSpan.FromMilliseconds(50);

    [DesktopFact]
    [Trait("Req", "TAC-002")]
    [Trait("Req", "EJE-003")]
    [Trait("Req", "NFR-001")]
    public async Task Taps_of_finger_pen_and_mouse_reach_the_engine_and_never_take_the_foreground()
    {
        var kinds = new[]
        {
            SyntheticPointerKind.Finger,
            SyntheticPointerKind.Pen,
            SyntheticPointerKind.Mouse,
        };
        var latencies = new List<(SyntheticPointerKind Kind, TimeSpan Latency)>();
        var violationsBefore = fixture.Lab.Guard.Violations;
        for (var tap = 0; tap < Taps; tap++)
        {
            var cursor = await fixture.PrepareAsync();
            var at = fixture.TileCenter(PanelTestData.Copy);
            using (var pointer = PanelDesktopFixture.CreatePointer(kinds[tap % kinds.Length]))
            {
                pointer.Tap(at.X, at.Y);
            }

            await PanelDesktopFixture.WaitUntilAsync(
                () => fixture.Engine.Count > 0,
                Describe(tap, "the tap never reached the engine")
            );
            var (posted, postedAt) = fixture.Engine.Posted[0];
            var activation = posted.ShouldBeOfType<EngineEvent.Activation>(
                Describe(tap, "an activation")
            );
            activation.Shortcut.Id.ShouldBe(PanelTestData.Copy);
            activation.Request.Phase.ShouldBe(ActivationPhase.ContactEnded);

            // From Windows recording the lift (the frame's performance counter is the tap's timestamp) to the mailbox,
            // as the gesture tests of M1 measure it: the synthetic gesture's own frames are not the panel's time.
            latencies.Add((kinds[tap % kinds.Length], postedAt - activation.Request.At));
            fixture.Probe.IsForeground.ShouldBeTrue(
                Describe(tap, "the probe keeps the foreground")
            );
            await fixture.Probe.PingAsync(
                PanelDesktopFixture.EventTimeout,
                TestContext.Current.CancellationToken
            );
            fixture
                .Probe.EventsSince(cursor)
                .OfType<FocusEvent>()
                .Where(static focus => !focus.IsGained)
                .ShouldBeEmpty(Describe(tap, "the probe never loses the keyboard focus"));
        }

        (fixture.Lab.Guard.Violations - violationsBefore).ShouldBe(0, "reg01.violations");
        fixture.Lab.Arbiter.Violations.ShouldBeEmpty();
        var all = latencies.Select(static sample => sample.Latency).ToList();
        var p95 = Percentile(all, 0.95);

        // Per device too: a slow path of one device (the pen's hover and leave frames, the mouse routed through
        // WM_POINTER) must be visible in the CI log without another run.
        var summary = string.Join(
            "; ",
            new[] { ("all", all) }
                .Concat(
                    kinds.Select(kind =>
                        (
                            kind.ToString(),
                            latencies
                                .Where(sample => sample.Kind == kind)
                                .Select(static sample => sample.Latency)
                                .ToList()
                        )
                    )
                )
                .Select(static group =>
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{group.Item1}: p50 {Percentile(group.Item2, 0.5).TotalMilliseconds:0.0} ms, p95 {Percentile(group.Item2, 0.95).TotalMilliseconds:0.0} ms, max {group.Item2.Max().TotalMilliseconds:0.0} ms"
                    )
                )
        );
        TestContext.Current.TestOutputHelper?.WriteLine(
            "Lift → engine mailbox over "
                + all.Count.ToString(CultureInfo.InvariantCulture)
                + " taps: "
                + summary
        );
        p95.ShouldBeLessThanOrEqualTo(PanelLatencyBudget, summary);
    }

    [DesktopFact]
    [Trait("Req", "EJE-004")]
    [Trait("Req", "EJE-006")]
    public async Task A_hold_presses_while_the_finger_rests_and_releases_its_own_contact_when_it_lifts()
    {
        _ = await fixture.PrepareAsync();
        var at = fixture.TileCenter(PanelTestData.HoldCtrl);

        using (var finger = PanelDesktopFixture.CreatePointer(SyntheticPointerKind.Finger))
        {
            finger.Hold(at.X, at.Y, TimeSpan.FromMilliseconds(500));
        }

        await PanelDesktopFixture.WaitUntilAsync(
            () => fixture.Engine.Count >= 2,
            "the hold's start and end never reached the engine"
        );
        var events = fixture.Engine.Events;
        var start = events[0].ShouldBeOfType<EngineEvent.Activation>();
        start.Shortcut.Id.ShouldBe(PanelTestData.HoldCtrl);
        start.Request.Phase.ShouldBe(ActivationPhase.ContactStarted);
        var end = events[1].ShouldBeOfType<EngineEvent.ContactEnded>();
        end.ContactId.ShouldBe(
            start.Request.ContactId!.Value,
            "the contact that pressed releases (INV-9)"
        );
        end.Cancelled.ShouldBeFalse("the finger lifted");
        end.Summary.Duration.ShouldBeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(400));
        fixture.Probe.IsForeground.ShouldBeTrue();
    }

    [DesktopFact]
    [Trait("Req", "EJE-006")]
    [Trait("Req", "SEG-007")]
    public async Task A_hold_whose_tile_leaves_the_panel_under_the_finger_still_releases_its_own_contact()
    {
        _ = await fixture.PrepareAsync();
        var at = fixture.TileCenter(PanelTestData.HoldCtrl);
        var profile = PanelTestData.Profile();
        var onlyCopy = profile with
        {
            Shortcuts = new ValueList<Clicalo.Domain.Library.Shortcut>([profile.Shortcuts[0]]),
        };

        try
        {
            using var finger = PanelDesktopFixture.CreatePointer(SyntheticPointerKind.Finger);
            var holding = Task.Run(
                () => finger.Hold(at.X, at.Y, TimeSpan.FromMilliseconds(900)),
                TestContext.Current.CancellationToken
            );
            await PanelDesktopFixture.WaitUntilAsync(
                () => fixture.Engine.Count > 0,
                "the hold never started"
            );

            // The tile that holds leaves the panel while the finger rests where it was (the grid keeps its columns,
            // so the finger stays on the panel): the lift must still reach the engine for that contact.
            WpfThread.Invoke(() =>
                fixture.ViewModel.Apply(PanelProjector.Project(onlyCopy, LangCode.Es, LangCode.Es))
            );
            await holding;
            await PanelDesktopFixture.WaitUntilAsync(
                () => fixture.Engine.Count >= 2,
                "the end of the hold never reached the engine"
            );

            var start = fixture.Engine.Events[0].ShouldBeOfType<EngineEvent.Activation>();
            start.Shortcut.Id.ShouldBe(PanelTestData.HoldCtrl);
            fixture
                .Engine.Events.OfType<EngineEvent.ContactEnded>()
                .ShouldContain(
                    ended => ended.ContactId == start.Request.ContactId,
                    "the contact that pressed releases (INV-9)"
                );
            fixture.Probe.IsForeground.ShouldBeTrue();
        }
        finally
        {
            WpfThread.Invoke(() =>
                fixture.ViewModel.Apply(PanelProjector.Project(profile, LangCode.Es, LangCode.Es))
            );
            WpfThread.Invoke(WpfThread.DrainPendingWork);
        }
    }

    [DesktopFact]
    [Trait("Req", "SEG-002")]
    [Trait("Req", "SEG-003")]
    [Trait("Req", "REG-03")]
    public async Task The_panic_strip_appears_while_something_is_held_and_its_button_releases_everything()
    {
        _ = await fixture.PrepareAsync();
        WpfThread.Invoke(() => fixture.ViewModel.ApplyEngine(Held(PanelTestData.ShiftLock)));
        WpfThread.Invoke(WpfThread.DrainPendingWork);
        WpfThread.Invoke(() => fixture.Window.IsPanicStripVisible).ShouldBeTrue();
        WpfThread
            .Invoke(() => fixture.Window.ReleaseAllButton.AccessibleName)
            .ShouldBe("Soltar todo");
        var at = fixture.ReleaseAllCenter();

        try
        {
            using (var finger = PanelDesktopFixture.CreatePointer(SyntheticPointerKind.Finger))
            {
                finger.Tap(at.X, at.Y);
            }

            await PanelDesktopFixture.WaitUntilAsync(
                () => fixture.Engine.Count > 0,
                "«Soltar todo» never reached the engine"
            );
            fixture.Engine.Events[0].ShouldBe(new EngineEvent.ReleaseAll(ReleaseReason.User));
            fixture.Probe.IsForeground.ShouldBeTrue();
        }
        finally
        {
            WpfThread.Invoke(() => fixture.ViewModel.ApplyEngine(EngineSnapshot.Empty));
            WpfThread.Invoke(WpfThread.DrainPendingWork);
        }

        WpfThread.Invoke(() => fixture.Window.IsPanicStripVisible).ShouldBeFalse();
    }

    [DesktopFact]
    [Trait("Req", "EJE-005")]
    [Trait("Req", "ACC-004")]
    public async Task Invoking_a_tile_through_UI_Automation_runs_it_without_touching_the_foreground()
    {
        _ = await fixture.PrepareAsync();
        var panel = WpfThread.Invoke(() => fixture.Window.SurfaceWindow.Handle);
        using var automation = new UIA3Automation();

        var tile = automation
            .FromHandle(panel)
            .FindFirstDescendant(condition => condition.ByName("Mayús fija"))
            .ShouldNotBeNull("the tile is in the UI Automation tree");
        tile.Patterns.Toggle.Pattern.Toggle();

        await PanelDesktopFixture.WaitUntilAsync(
            () => fixture.Engine.Count > 0,
            "the UI Automation toggle never reached the engine"
        );
        var activation = fixture.Engine.Events[0].ShouldBeOfType<EngineEvent.Activation>();
        activation.Shortcut.Id.ShouldBe(PanelTestData.ShiftLock);
        activation.Request.Origin.ShouldBe(ActivationOrigin.UiaInvoke);
        activation.Request.ContactId.ShouldBeNull();
        fixture.Probe.IsForeground.ShouldBeTrue(
            "invoking by UI Automation never activates the panel (UIA009)"
        );
    }

    [DesktopFact]
    [Trait("Req", "BUR-003")]
    [Trait("Req", "SEG-007")]
    public async Task Hiding_the_panel_releases_everything_and_never_takes_the_foreground()
    {
        _ = await fixture.PrepareAsync();

        try
        {
            WpfThread.Invoke(fixture.Visibility.Toggle);
            WpfThread.Invoke(WpfThread.DrainPendingWork);

            WpfThread.Invoke(() => fixture.Window.IsVisible).ShouldBeFalse();
            fixture
                .Engine.Events.ShouldHaveSingleItem()
                .ShouldBe(new EngineEvent.Terminal(TerminalReason.Hide));
            fixture.Probe.IsForeground.ShouldBeTrue();
        }
        finally
        {
            WpfThread.Invoke(fixture.Visibility.Show);
            WpfThread.Invoke(WpfThread.DrainPendingWork);
        }

        WpfThread.Invoke(() => fixture.Window.IsVisible).ShouldBeTrue();
        WpfThread.Invoke(() => fixture.Session.Current.Presence).ShouldBe(PanelPresence.Visible);
        fixture.Probe.IsForeground.ShouldBeTrue();
    }

    private static EngineSnapshot Held(ShortcutId shortcut) =>
        EngineSnapshot.Empty with
        {
            Held = new ValueList<PressedItem>([
                new PressedItem(
                    HolderId.ForToggle(shortcut),
                    HoldOrigin.Toggle,
                    shortcut,
                    null,
                    [],
                    MouseButtons.None,
                    0,
                    null
                ),
            ]),
            Version = 1,
        };

    private static TimeSpan Percentile(List<TimeSpan> values, double percentile)
    {
        var sorted = values.Order().ToList();
        var index = (int)Math.Ceiling(percentile * sorted.Count) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
    }

    private static string Describe(int tap, string what) =>
        string.Create(CultureInfo.InvariantCulture, $"tap {tap + 1} of {Taps}: {what}; ")
        + ForegroundWindows.Describe();
}
