using Clicalo.Application.Ports;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Windowing;
using Clicalo.Windowing.IntegrationTests.Windowing.Support;

namespace Clicalo.Windowing.IntegrationTests.Windowing;

/// <summary>
/// <see cref="ActivationGuard"/> fed by the surfaces' own activation messages (blueprint §3.5). Headless: the messages
/// are sent to surfaces that are never shown, so no window is really activated; the violations are expected and their
/// <c>Debug.Fail</c> is recorded (<see cref="DebugFailures"/>).
/// </summary>
public sealed class ActivationGuardTests
{
    [Fact]
    [Trait("Req", "REG-01")]
    public void An_activation_without_a_lease_is_one_violation_until_the_surface_is_deactivated()
    {
        using var failures = DebugFailures.Capture();
        using var lab = SurfaceLab.Create();
        var surface = SurfaceLab.WithHandle(lab.CreateSurface(SurfaceKind.Panel, 0, 200, 100));
        var window = surface.Handle;
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

        Send(first.Handle, NativeSurface.WmActivate, NativeSurface.Active);
        lab.Close(first);
        Send(second.Handle, NativeSurface.WmActivate, NativeSurface.Active);

        lab.Guard.Violations.ShouldBe(2);
        lab.Arbiter.Violations.Select(violation => violation.Surface)
            .ShouldBe([first.Id, second.Id]);
    }

    private static nint Send(nint window, uint message, nint wParam) =>
        NativeSurface.SendMessageW(window, message, wParam, 0);
}
