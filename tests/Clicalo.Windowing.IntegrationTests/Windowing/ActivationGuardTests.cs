using Clicalo.Application.Ports;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Windowing;
using Clicalo.Windowing.IntegrationTests.Windowing.Support;

namespace Clicalo.Windowing.IntegrationTests.Windowing;

/// <summary>
/// <see cref="ActivationGuard"/> fed by the surfaces' own activation messages (blueprint §3.5, ADR-0024). Headless and
/// deterministic: the messages are sent to surfaces that are never shown, so no window is really activated and the lab
/// tells the guard which window owns the foreground (<see cref="SurfaceLab.SimulatedForeground"/>); each
/// <see cref="WpfThread.Invoke(Action)"/> is one retrieval of the UI thread, and the violations are expected, so their
/// <c>Debug.Fail</c> is recorded (<see cref="DebugFailures"/>).
/// </summary>
[Trait("Req", "REG-01")]
public sealed class ActivationGuardTests
{
    [Fact]
    public void An_activation_without_a_lease_asks_for_one_restore()
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

        // One activation: WM_ACTIVATEAPP, WM_NCACTIVATE and WM_ACTIVATE inside one retrieval of the UI thread.
        WpfThread.Invoke(() =>
        {
            Send(window, NativeSurface.WmActivateApp, NativeSurface.Active);
            Send(window, NativeSurface.WmNcActivate, NativeSurface.Active);
            Send(window, NativeSurface.WmActivate, NativeSurface.Active)
                .ShouldBe(0, "the violating WM_ACTIVATE is kept from WPF and DefWindowProc");
            lab.Arbiter.Violations.ShouldBeEmpty("nothing is asked inside the window procedure");
        });
        WpfThread.Invoke(WpfThread.DrainPendingWork);

        lab.Guard.Violations.ShouldBe(1);
        var violation = lab.Arbiter.Violations.ShouldHaveSingleItem();
        violation.Surface.ShouldBe(surface.Id);
        violation.Window.ShouldBe(surface.SurfaceWindow);
        violation.Message.ShouldBe(ActivationMessage.ActivateApp);
        violation.ProbableCause.ShouldBe(ActivationCause.External);
        NativeSurface
            .HasExStyle(window, NativeSurface.ExNoActivate)
            .ShouldBeTrue("the guard applies WS_EX_NOACTIVATE again");
        failures.Messages.Count.ShouldBe(DebugFailures.AreLive ? 1 : 0);
    }

    [Fact]
    [Trait("Req", "BUS-002")]
    public void An_activation_with_a_lease_asks_for_nothing()
    {
        using var lab = SurfaceLab.Create();
        var surface = SurfaceLab.WithHandle(lab.CreateSurface(SurfaceKind.Panel, 0, 200, 100));
        lab.SimulatedForeground = surface.Handle;
        lab.Arbiter.Lease(surface.SurfaceWindow);

        WpfThread.Invoke(() =>
        {
            Send(surface.Handle, NativeSurface.WmActivateApp, NativeSurface.Active);
            Send(surface.Handle, NativeSurface.WmNcActivate, NativeSurface.Active);
            Send(surface.Handle, NativeSurface.WmActivate, NativeSurface.Active);
        });
        WpfThread.Invoke(WpfThread.DrainPendingWork);

        lab.Guard.Violations.ShouldBe(0);
        lab.Arbiter.Violations.ShouldBeEmpty();
        WpfThread.Invoke(() => surface.IsActive).ShouldBeTrue("the leased activation reaches WPF");
    }

    [Fact]
    public void Activations_while_a_restore_is_queued_join_it()
    {
        using var failures = DebugFailures.Capture();
        using var lab = SurfaceLab.Create();
        var panel = SurfaceLab.WithHandle(lab.CreateSurface(SurfaceKind.Panel, 0, 200, 100));
        var bubble = SurfaceLab.WithHandle(lab.CreateSurface(SurfaceKind.Bubble, 0, 64, 64));

        // Two activations, of two surfaces, before the UI thread is back in its dispatcher.
        WpfThread.Invoke(() =>
        {
            lab.SimulatedForeground = panel.Handle;
            Send(panel.Handle, NativeSurface.WmNcActivate, NativeSurface.Active);
            Send(panel.Handle, NativeSurface.WmActivate, NativeSurface.Active);
            Send(panel.Handle, NativeSurface.WmActivate, NativeSurface.Inactive);
            lab.SimulatedForeground = bubble.Handle;
            Send(bubble.Handle, NativeSurface.WmNcActivate, NativeSurface.Active);
        });
        WpfThread.Invoke(WpfThread.DrainPendingWork);

        lab.Guard.Violations.ShouldBe(1);
        lab.Arbiter.Violations.ShouldHaveSingleItem().Surface.ShouldBe(panel.Id);

        // Once the request has left, the next activation asks again.
        WpfThread.Invoke(() =>
            Send(bubble.Handle, NativeSurface.WmNcActivate, NativeSurface.Active)
        );
        WpfThread.Invoke(WpfThread.DrainPendingWork);

        lab.Guard.Violations.ShouldBe(2);
        lab.Arbiter.Violations.Select(violation => violation.Surface)
            .ShouldBe([panel.Id, bubble.Id]);
        failures.Messages.Count.ShouldBe(DebugFailures.AreLive ? 2 : 0);
    }

    [Fact]
    [Trait("Req", "CCM-004")]
    public void The_application_activation_counts_only_on_the_surface_in_front()
    {
        // WM_ACTIVATEAPP(TRUE) reaches every top-level window of the thread, also when the Control Center is the one
        // being activated under its lease; on a surface that does not own the foreground it is not a violation.
        using var lab = SurfaceLab.Create();
        var window = SurfaceLab.WithHandle(lab.CreateSurface(SurfaceKind.Bubble, 0, 64, 64)).Handle;

        WpfThread.Invoke(() => Send(window, NativeSurface.WmActivateApp, NativeSurface.Active));
        WpfThread.Invoke(WpfThread.DrainPendingWork);

        lab.Guard.Violations.ShouldBe(0);
        lab.Arbiter.Violations.ShouldBeEmpty();
    }

    [Fact]
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
        WpfThread.Invoke(WpfThread.DrainPendingWork);

        lab.Arbiter.Violations.Select(violation => violation.ProbableCause)
            .ShouldBe([
                ActivationCause.DpiChange,
                ActivationCause.External,
                ActivationCause.Unknown,
            ]);
    }

    [Fact]
    [Trait("Req", "BUS-002")]
    public void A_leased_activation_after_a_violation_that_was_never_deactivated_reaches_WPF_whole()
    {
        // The restore of a violation may never deactivate the panel (spike S1), so the WA_INACTIVE that would match its
        // kept WM_ACTIVATE never comes. A later activation under a lease (text input) is WPF's to see: so is its end, or
        // WPF keeps the panel active after the lease.
        using var failures = DebugFailures.Capture();
        using var lab = SurfaceLab.Create();
        var surface = SurfaceLab.WithHandle(lab.CreateSurface(SurfaceKind.Panel, 0, 200, 100));
        var panel = surface.Handle;

        lab.SimulatedForeground = panel;
        WpfThread.Invoke(() => Send(panel, NativeSurface.WmActivate, NativeSurface.Active));
        WpfThread.Invoke(WpfThread.DrainPendingWork);
        lab.Guard.Violations.ShouldBe(1);
        WpfThread
            .Invoke(() => surface.IsActive)
            .ShouldBeFalse("the violating WM_ACTIVATE is kept from WPF");

        lab.Arbiter.Lease(surface.SurfaceWindow);
        WpfThread.Invoke(() => Send(panel, NativeSurface.WmActivate, NativeSurface.Active));
        WpfThread.Invoke(() => surface.IsActive).ShouldBeTrue("the leased activation reaches WPF");

        lab.Arbiter.EndLease(surface.SurfaceWindow);
        WpfThread.Invoke(() => Send(panel, NativeSurface.WmActivate, NativeSurface.Inactive));

        WpfThread
            .Invoke(() => surface.IsActive)
            .ShouldBeFalse("WPF sees the end of the activation it saw begin");
        lab.Guard.Violations.ShouldBe(1);
    }

    private static nint Send(nint window, uint message, nint wParam) =>
        NativeSurface.SendMessageW(window, message, wParam, 0);
}
