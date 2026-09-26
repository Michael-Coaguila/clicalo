using Clicalo.Application.Ports;
using Clicalo.Domain.Timing;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Windowing;
using Clicalo.Windowing.IntegrationTests.Windowing.Support;
using Microsoft.Extensions.Time.Testing;

namespace Clicalo.Windowing.IntegrationTests.Windowing;

/// <summary>
/// <see cref="ActivationGuard"/> fed by the surfaces' own activation messages (blueprint §3.5). Headless: the messages
/// are sent to surfaces that are never shown, so no window is really activated and the lab tells the guard which window
/// owns the foreground (<see cref="SurfaceLab.SimulatedForeground"/>); the violations are expected and their
/// <c>Debug.Fail</c> is recorded (<see cref="DebugFailures"/>).
/// </summary>
public sealed class ActivationGuardTests
{
    /// <summary>A window of another application, as the guard sees the foreground (never dereferenced).</summary>
    private const nint AnotherApp = 0x7FFF_0010;

    [Fact]
    [Trait("Req", "REG-01")]
    public void An_activation_without_a_lease_is_one_violation_until_the_surface_is_deactivated()
    {
        using var failures = DebugFailures.Capture();
        using var lab = SurfaceLab.Create();
        var surface = SurfaceLab.WithHandle(lab.CreateSurface(SurfaceKind.Panel, 0, 200, 100));
        var window = surface.Handle;
        lab.SimulatedForeground = window;
        NativeSurface.SetExStyle(
            window,
            NativeSurface.ExStyle(window) & ~NativeSurface.ExNoActivate
        );

        Send(window, NativeSurface.WmActivate, NativeSurface.Active)
            .ShouldBe(
                0,
                "The violating WM_ACTIVATE is kept from WPF and DefWindowProc (no focus moves in)."
            );

        lab.Guard.Violations.ShouldBe(1);
        var violation = lab.Arbiter.Violations.ShouldHaveSingleItem();
        violation.Surface.ShouldBe(surface.Id);
        violation.Window.ShouldBe(surface.SurfaceWindow);
        violation.Message.ShouldBe(ActivationMessage.Activate);
        violation.ProbableCause.ShouldBe(ActivationCause.Unknown);
        NativeSurface
            .HasExStyle(window, NativeSurface.ExNoActivate)
            .ShouldBeTrue("The guard applies WS_EX_NOACTIVATE again.");

        // The rest of the same activation does not count again.
        Send(window, NativeSurface.WmNcActivate, NativeSurface.Active);
        Send(window, NativeSurface.WmActivate, NativeSurface.Active);
        lab.Guard.Violations.ShouldBe(1);

        // Once deactivated, a new activation is a new violation.
        Send(window, NativeSurface.WmActivate, NativeSurface.Inactive);
        Send(window, NativeSurface.WmNcActivate, NativeSurface.Active);
        lab.Guard.Violations.ShouldBe(2);
        lab.Arbiter.Violations[^1].Message.ShouldBe(ActivationMessage.NcActivate);

        // Losing the application activation also ends it.
        Send(window, NativeSurface.WmActivateApp, NativeSurface.Inactive);
        Send(window, NativeSurface.WmActivate, NativeSurface.Active);
        lab.Guard.Violations.ShouldBe(3);

        failures.Messages.Count.ShouldBe(DebugFailures.AreLive ? 3 : 0);
        if (DebugFailures.AreLive)
        {
            failures.Messages.ShouldAllBe(message =>
                message.StartsWith("REG-01 violation", StringComparison.Ordinal)
            );
        }
    }

    [Fact]
    [Trait("Req", "REG-01")]
    [Trait("Req", "BUS-002")]
    public void A_leased_activation_is_legitimate()
    {
        using var lab = SurfaceLab.Create();
        var surface = SurfaceLab.WithHandle(lab.CreateSurface(SurfaceKind.Panel, 0, 200, 100));
        lab.Arbiter.Lease(surface.SurfaceWindow);

        var violation = WpfThread.Invoke(() =>
            lab.Guard.OnActivated(surface, ActivationMessage.Activate, ActivationCause.Unknown)
        );

        violation.ShouldBeFalse();
        lab.Guard.Violations.ShouldBe(0);
        lab.Arbiter.Violations.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "REG-01")]
    [Trait("Req", "CCM-004")]
    public void The_application_activation_counts_only_on_the_surface_in_front()
    {
        // WM_ACTIVATEAPP(TRUE) reaches every top-level window of the thread, also when the Control Center is the one
        // being activated under its lease; on a surface that does not own the foreground it is not a violation.
        using var lab = SurfaceLab.Create();
        var window = SurfaceLab.WithHandle(lab.CreateSurface(SurfaceKind.Bubble, 0, 64, 64)).Handle;

        Send(window, NativeSurface.WmActivateApp, NativeSurface.Active);

        lab.Guard.Violations.ShouldBe(0);
    }

    [Fact]
    [Trait("Req", "REG-01")]
    public void The_probable_cause_is_what_the_surfaces_were_doing()
    {
        using var failures = DebugFailures.Capture();
        using var lab = SurfaceLab.Create();
        var surface = SurfaceLab.WithHandle(lab.CreateSurface(SurfaceKind.Panel, 0, 200, 100));
        var window = surface.Handle;
        lab.SimulatedForeground = window;

        // One dispatcher operation each, as one message retrieval delivers one activation sequence.
        WpfThread.Invoke(() =>
        {
            var dpi = NativeSurface.GetDpiForWindow(window);
            var bounds = NativeSurface.Bounds(window);
            var newDpi = dpi + (dpi / 2);
            var suggested = bounds with
            {
                Right = bounds.Left + (int)(bounds.Width * newDpi / dpi),
                Bottom = bounds.Top + (int)(bounds.Height * newDpi / dpi),
            };
            _ = NativeSurface.SendWithStructure(
                window,
                NativeSurface.WmDpiChanged,
                (nint)((newDpi << 16) | newDpi),
                suggested
            );
            Send(window, NativeSurface.WmActivate, NativeSurface.Active);
            Send(window, NativeSurface.WmActivate, NativeSurface.Inactive);
        });
        WpfThread.Invoke(() =>
        {
            Send(window, NativeSurface.WmActivateApp, NativeSurface.Active);
            Send(window, NativeSurface.WmActivate, NativeSurface.Active);
            Send(window, NativeSurface.WmActivate, NativeSurface.Inactive);
        });
        WpfThread.Invoke(() =>
        {
            Send(window, NativeSurface.WmActivate, NativeSurface.Active);
            Send(window, NativeSurface.WmActivate, NativeSurface.Inactive);
        });

        lab.Arbiter.Violations.Select(violation => violation.ProbableCause)
            .ShouldBe([
                ActivationCause.DpiChange,
                ActivationCause.External,
                ActivationCause.Unknown,
            ]);
    }

    [Fact]
    [Trait("Req", "REG-01")]
    public void A_destroyed_surface_ends_its_open_violation()
    {
        using var failures = DebugFailures.Capture();
        using var lab = SurfaceLab.Create();
        var first = SurfaceLab.WithHandle(lab.CreateSurface(SurfaceKind.Menu, 0, 120, 80));
        var second = SurfaceLab.WithHandle(lab.CreateSurface(SurfaceKind.Menu, 1, 120, 80));

        lab.SimulatedForeground = first.Handle;
        Send(first.Handle, NativeSurface.WmActivate, NativeSurface.Active);
        lab.Close(first);
        lab.SimulatedForeground = second.Handle;
        Send(second.Handle, NativeSurface.WmActivate, NativeSurface.Active);

        lab.Guard.Violations.ShouldBe(2);
        lab.Arbiter.Violations.Select(violation => violation.Surface)
            .ShouldBe([first.Id, second.Id]);
    }

    [Fact]
    [Trait("Req", "REG-01")]
    public void A_late_activation_message_after_the_restore_is_kept_but_not_counted()
    {
        using var failures = DebugFailures.Capture();
        var clock = new FakeTimeProvider();
        using var lab = SurfaceLab.Create(timeProvider: clock);
        var panel = SurfaceLab.WithHandle(lab.CreateSurface(SurfaceKind.Panel, 0, 200, 100)).Handle;
        var bubble = SurfaceLab.WithHandle(lab.CreateSurface(SurfaceKind.Bubble, 0, 64, 64)).Handle;

        // One forced activation: WM_ACTIVATEAPP(TRUE) to every window of the thread, then the activation of the panel.
        lab.SimulatedForeground = panel;
        Send(panel, NativeSurface.WmActivateApp, NativeSurface.Active);
        Send(bubble, NativeSurface.WmActivateApp, NativeSurface.Active);
        Send(panel, NativeSurface.WmNcActivate, NativeSurface.Active);
        Send(panel, NativeSurface.WmActivate, NativeSurface.Active);
        lab.Guard.Violations.ShouldBe(1, "one activation is one violation");

        // The restore gives the foreground back from the thread pool and the application is deactivated; a message of
        // the activation that is only delivered now finds the foreground in another application.
        lab.SimulatedForeground = AnotherApp;
        Send(panel, NativeSurface.WmActivateApp, NativeSurface.Inactive);
        Send(bubble, NativeSurface.WmActivateApp, NativeSurface.Inactive);
        NativeSurface.SetExStyle(panel, NativeSurface.ExStyle(panel) & ~NativeSurface.ExNoActivate);
        Send(panel, NativeSurface.WmNcActivate, NativeSurface.Active);
        Send(panel, NativeSurface.WmActivate, NativeSurface.Active)
            .ShouldBe(0, "the late WM_ACTIVATE is still kept from WPF and DefWindowProc");
        NativeSurface
            .HasExStyle(panel, NativeSurface.ExNoActivate)
            .ShouldBeTrue("the guard applies WS_EX_NOACTIVATE again");

        // Its deferred judgment still finds the foreground in the other application, then and after the recheck.
        LetTheGuardJudge(clock);

        lab.Guard.Violations.ShouldBe(1, "a late message is not a second violation");
        lab.Arbiter.Violations.Count.ShouldBe(1);

        // The next forced activation is judged on its own, once.
        lab.SimulatedForeground = panel;
        Send(bubble, NativeSurface.WmActivateApp, NativeSurface.Active);
        Send(panel, NativeSurface.WmActivateApp, NativeSurface.Active);
        Send(panel, NativeSurface.WmNcActivate, NativeSurface.Active);
        Send(panel, NativeSurface.WmActivate, NativeSurface.Active);
        LetTheGuardJudge(clock);

        lab.Guard.Violations.ShouldBe(2);
        lab.Arbiter.Violations.Count.ShouldBe(2);
        failures.Messages.Count.ShouldBe(DebugFailures.AreLive ? 2 : 0);
    }

    [Fact]
    [Trait("Req", "REG-01")]
    public void An_activation_that_the_foreground_confirms_later_is_one_violation()
    {
        // Spike S1: a forced activation reached the panel as a lone WM_ACTIVATE while GetForegroundWindow still named
        // another application, and the foreground then stayed in this process. It must be detected and reported.
        using var failures = DebugFailures.Capture();
        var clock = new FakeTimeProvider();
        using var lab = SurfaceLab.Create(timeProvider: clock);
        var surface = SurfaceLab.WithHandle(lab.CreateSurface(SurfaceKind.Panel, 0, 200, 100));
        var panel = surface.Handle;

        // Confirmed once the thread has delivered what it had queued.
        lab.SimulatedForeground = AnotherApp;
        WpfThread.Invoke(() =>
        {
            Send(panel, NativeSurface.WmActivate, NativeSurface.Active)
                .ShouldBe(0, "the unconfirmed WM_ACTIVATE is kept from WPF");
            lab.Guard.Violations.ShouldBe(0, "it is not judged inside the window procedure");
            lab.SimulatedForeground = panel;
        });
        WpfThread.Invoke(WpfThread.DrainPendingWork);

        lab.Guard.Violations.ShouldBe(1);
        var first = lab.Arbiter.Violations.ShouldHaveSingleItem();
        first.Surface.ShouldBe(surface.Id);
        first.Message.ShouldBe(ActivationMessage.Activate);
        first.ProbableCause.ShouldBe(
            ActivationCause.External,
            "the foreground was in another application when it arrived"
        );

        // The rest of that activation does not count again; the end of the application activation closes it.
        Send(panel, NativeSurface.WmNcActivate, NativeSurface.Active);
        lab.Guard.Violations.ShouldBe(1);
        Send(panel, NativeSurface.WmActivateApp, NativeSurface.Inactive);

        // Confirmed only by the recheck: the foreground reaches the owner anchor of the surfaces, not the panel.
        lab.SimulatedForeground = AnotherApp;
        Send(panel, NativeSurface.WmNcActivate, NativeSurface.Active);
        WpfThread.Invoke(WpfThread.DrainPendingWork);
        lab.Guard.Violations.ShouldBe(1, "the foreground is still in the other application");
        lab.SimulatedForeground = WpfThread.Invoke(() => lab.Anchor.EnsureCreated().Handle);
        LetTheGuardJudge(clock);

        lab.Guard.Violations.ShouldBe(2);
        lab.Arbiter.Violations[^1].Message.ShouldBe(ActivationMessage.NcActivate);
        failures.Messages.Count.ShouldBe(DebugFailures.AreLive ? 2 : 0);
    }

    [Fact]
    [Trait("Req", "REG-01")]
    public void A_violation_ends_when_the_foreground_is_seen_outside_the_process()
    {
        // Spike S1, cycle 7 of a Debug run: the deactivation of cycle 6 never arrived, and the lone WM_NCACTIVATE(TRUE)
        // of the next forced activation, with the panel in front, was swallowed as part of the open violation.
        using var failures = DebugFailures.Capture();
        var clock = new FakeTimeProvider();
        using var lab = SurfaceLab.Create(timeProvider: clock);
        var panel = SurfaceLab.WithHandle(lab.CreateSurface(SurfaceKind.Panel, 0, 200, 100)).Handle;

        lab.SimulatedForeground = panel;
        Send(panel, NativeSurface.WmNcActivate, NativeSurface.Active);
        lab.Guard.Violations.ShouldBe(1);

        // A stale deactivation delivered while the panel is still in front does not end it.
        Send(panel, NativeSurface.WmNcActivate, NativeSurface.Inactive);
        Send(panel, NativeSurface.WmActivate, NativeSurface.Active);
        lab.Guard.Violations.ShouldBe(1, "the same activation");

        // The restore worked, but no deactivation message arrives: the watch sees the foreground outside.
        lab.SimulatedForeground = AnotherApp;
        LetTheGuardJudge(clock);
        lab.SimulatedForeground = panel;
        Send(panel, NativeSurface.WmNcActivate, NativeSurface.Active);
        lab.Guard.Violations.ShouldBe(2, "the next forced activation is not swallowed");
        lab.Arbiter.Violations[^1]
            .ProbableCause.ShouldBe(
                ActivationCause.External,
                "the foreground was last seen in another application"
            );

        // A deactivation message seen with the foreground outside ends it at once.
        lab.SimulatedForeground = AnotherApp;
        Send(panel, NativeSurface.WmNcActivate, NativeSurface.Inactive);
        lab.SimulatedForeground = panel;
        Send(panel, NativeSurface.WmNcActivate, NativeSurface.Active);

        lab.Guard.Violations.ShouldBe(3);
        failures.Messages.Count.ShouldBe(DebugFailures.AreLive ? 3 : 0);
    }

    [Fact]
    [Trait("Req", "REG-01")]
    [Trait("Req", "CCM-004")]
    public void An_activation_while_another_window_of_the_process_is_in_front_counts_at_once()
    {
        using var failures = DebugFailures.Capture();
        var clock = new FakeTimeProvider();
        using var lab = SurfaceLab.Create(timeProvider: clock);
        var panel = SurfaceLab.WithHandle(lab.CreateSurface(SurfaceKind.Panel, 0, 200, 100)).Handle;
        var bubble = SurfaceLab.WithHandle(lab.CreateSurface(SurfaceKind.Bubble, 0, 64, 64));

        lab.SimulatedForeground = bubble.Handle;
        Send(panel, NativeSurface.WmActivate, NativeSurface.Active);

        lab.Guard.Violations.ShouldBe(1, "the foreground was taken from the app in front");

        // Unless a lease lets that window activate (the Control Center under its lease): then it waits for the
        // deferred judgment, which finds the leased window and counts nothing.
        Send(panel, NativeSurface.WmActivateApp, NativeSurface.Inactive);
        lab.Arbiter.Lease(bubble.SurfaceWindow);
        Send(panel, NativeSurface.WmActivate, NativeSurface.Active);
        LetTheGuardJudge(clock);

        lab.Guard.Violations.ShouldBe(1);
        failures.Messages.Count.ShouldBe(DebugFailures.AreLive ? 1 : 0);
    }

    [Fact]
    [Trait("Req", "REG-01")]
    public void A_violation_whose_end_never_arrived_ends_with_the_next_activation_of_the_application()
    {
        using var failures = DebugFailures.Capture();
        using var lab = SurfaceLab.Create();
        var panel = SurfaceLab.WithHandle(lab.CreateSurface(SurfaceKind.Panel, 0, 200, 100)).Handle;

        // An activation inside the application (no WM_ACTIVATEAPP) whose deactivation never reaches the panel.
        lab.SimulatedForeground = panel;
        Send(panel, NativeSurface.WmNcActivate, NativeSurface.Active);
        lab.Guard.Violations.ShouldBe(1);

        // The next activation of the application is judged on its own instead of being swallowed as part of it.
        Send(panel, NativeSurface.WmActivateApp, NativeSurface.Active);
        Send(panel, NativeSurface.WmNcActivate, NativeSurface.Active);
        Send(panel, NativeSurface.WmActivate, NativeSurface.Active);

        lab.Guard.Violations.ShouldBe(2);
        lab.Arbiter.Violations.Select(violation => violation.Message)
            .ShouldBe([ActivationMessage.NcActivate, ActivationMessage.ActivateApp]);
        failures.Messages.Count.ShouldBe(DebugFailures.AreLive ? 2 : 0);
    }

    /// <summary>
    /// Runs the deferred judgment of the guard to the end: the look queued behind the work of the WPF thread, then the
    /// recheck after <c>Timings.Windowing.ActivationRecheck</c> on <paramref name="clock"/>.
    /// </summary>
    private static void LetTheGuardJudge(FakeTimeProvider clock)
    {
        WpfThread.Invoke(WpfThread.DrainPendingWork);
        clock.Advance(Timings.Windowing.ActivationRecheck);
        WpfThread.Invoke(WpfThread.DrainPendingWork);
    }

    private static nint Send(nint window, uint message, nint wParam) =>
        NativeSurface.SendMessageW(window, message, wParam, 0);
}
