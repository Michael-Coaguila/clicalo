using Clicalo.Application.Ports;
using Clicalo.Domain.Timing;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Windowing;
using Clicalo.Windowing.IntegrationTests.Windowing.Support;
using Microsoft.Extensions.Time.Testing;

namespace Clicalo.Windowing.IntegrationTests.Windowing;

/// <summary>
/// <see cref="SurfaceIntegrityCheck"/> puts back what drifts (blueprint §3.5). Headless: the drift is made from outside
/// on surfaces that are never shown.
/// </summary>
public sealed class SurfaceIntegrityCheckTests
{
    [Fact]
    [Trait("Req", "REG-01")]
    public void A_check_puts_back_WS_EX_NOACTIVATE_and_the_topmost_band()
    {
        using var lab = SurfaceLab.Create();
        var surface = SurfaceLab.WithHandle(lab.CreateSurface(SurfaceKind.Panel, 0, 200, 100));
        var repaired = new List<SurfaceRepairKind>();
        lab.Integrity.Repaired += (_, e) => repaired.Add(e.Kind);
        Drift(surface);

        WpfThread.Invoke(lab.Integrity.CheckNow).ShouldBe(2);

        repaired.ShouldBe([SurfaceRepairKind.NoActivateStyle, SurfaceRepairKind.Topmost]);
        lab.Integrity.Repairs.ShouldBe(2);
        NativeSurface
            .HasExStyle(surface.Handle, NativeSurface.ExNoActivate | NativeSurface.ExTopmost)
            .ShouldBeTrue();
        NativeSurface
            .IsWindowVisible(surface.Handle)
            .ShouldBeFalse("A repair never shows the surface.");
        WpfThread.Invoke(lab.Integrity.CheckNow).ShouldBe(0, "Nothing is left to repair.");
    }

    [Fact]
    [Trait("Req", "REG-01")]
    public void One_surface_leaving_the_topmost_band_takes_its_siblings_and_all_are_repaired()
    {
        // SetWindowPos: "when a topmost window is made non-topmost, its owners and its owned windows are also made
        // non-topmost". Every surface is owned by the same OwnerAnchor, so they leave the band together (S1 finding).
        using var lab = SurfaceLab.Create();
        var panel = SurfaceLab.WithHandle(lab.CreateSurface(SurfaceKind.Panel, 0, 200, 100));
        var bubble = SurfaceLab.WithHandle(lab.CreateSurface(SurfaceKind.Bubble, 0, 64, 64));

        NativeSurface
            .SetWindowPos(
                panel.Handle,
                NativeSurface.NotTopmostBand,
                0,
                0,
                0,
                0,
                NativeSurface.NoMove | NativeSurface.NoSize | NativeSurface.NoActivate
            )
            .ShouldBeTrue();

        NativeSurface.HasExStyle(bubble.Handle, NativeSurface.ExTopmost).ShouldBeFalse();
        WpfThread.Invoke(lab.Integrity.CheckNow).ShouldBe(2);
        NativeSurface.HasExStyle(panel.Handle, NativeSurface.ExTopmost).ShouldBeTrue();
        NativeSurface.HasExStyle(bubble.Handle, NativeSurface.ExTopmost).ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "REG-01")]
    [Trait("Req", "BUS-002")]
    public void A_surface_under_a_lease_keeps_its_lease_style()
    {
        using var lab = SurfaceLab.Create();
        var surface = SurfaceLab.WithHandle(lab.CreateSurface(SurfaceKind.Panel, 0, 200, 100));
        lab.Registry.AllowActivation(surface.Id);

        WpfThread.Invoke(lab.Integrity.CheckNow).ShouldBe(0);

        NativeSurface.HasExStyle(surface.Handle, NativeSurface.ExNoActivate).ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "REG-01")]
    public void The_check_runs_every_interval()
    {
        var time = new FakeTimeProvider();
        using var lab = SurfaceLab.Create(timeProvider: time);
        var surface = SurfaceLab.WithHandle(lab.CreateSurface(SurfaceKind.Bubble, 0, 64, 64));
        WpfThread.Invoke(lab.Integrity.Start);
        Drift(surface);

        time.Advance(Timings.Foreground.SurfaceIntegrityInterval - TimeSpan.FromTicks(1));
        WpfThread.Invoke(WpfThread.DrainPendingWork);
        lab.Integrity.Repairs.ShouldBe(0);

        time.Advance(TimeSpan.FromTicks(1));
        WpfThread.Invoke(WpfThread.DrainPendingWork);
        lab.Integrity.Repairs.ShouldBe(2);
        NativeSurface
            .HasExStyle(surface.Handle, NativeSurface.ExNoActivate | NativeSurface.ExTopmost)
            .ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "REG-01")]
    [Trait("Req", "ACC-008")]
    public void A_display_change_triggers_a_check()
    {
        var time = new FakeTimeProvider();
        using var lab = SurfaceLab.Create(timeProvider: time);
        var surface = SurfaceLab.WithHandle(lab.CreateSurface(SurfaceKind.Dock, 0, 48, 240));
        WpfThread.Invoke(lab.Integrity.Start);
        Drift(surface);

        _ = NativeSurface.SendMessageW(surface.Handle, NativeSurface.WmDisplayChange, 32, 0);
        WpfThread.Invoke(WpfThread.DrainPendingWork);

        lab.Integrity.Repairs.ShouldBe(2);
    }

    [Fact]
    [Trait("Req", "REG-01")]
    public void A_stopped_check_repairs_nothing_by_itself()
    {
        var time = new FakeTimeProvider();
        using var lab = SurfaceLab.Create(timeProvider: time);
        var surface = SurfaceLab.WithHandle(lab.CreateSurface(SurfaceKind.Panel, 0, 200, 100));
        WpfThread.Invoke(lab.Integrity.Start);
        WpfThread.Invoke(lab.Integrity.Dispose);
        Drift(surface);

        time.Advance(Timings.Foreground.SurfaceIntegrityInterval);
        _ = NativeSurface.SendMessageW(surface.Handle, NativeSurface.WmDisplayChange, 32, 0);
        WpfThread.Invoke(WpfThread.DrainPendingWork);

        lab.Integrity.Repairs.ShouldBe(0);
    }

    /// <summary>Removes <c>WS_EX_NOACTIVATE</c> and takes the surface out of the topmost band, from outside.</summary>
    private static void Drift(TestSurface surface)
    {
        var window = surface.Handle;
        NativeSurface.SetExStyle(
            window,
            NativeSurface.ExStyle(window) & ~NativeSurface.ExNoActivate
        );
        NativeSurface
            .SetWindowPos(
                window,
                NativeSurface.NotTopmostBand,
                0,
                0,
                0,
                0,
                NativeSurface.NoMove | NativeSurface.NoSize | NativeSurface.NoActivate
            )
            .ShouldBeTrue();
        NativeSurface
            .HasExStyle(window, NativeSurface.ExTopmost)
            .ShouldBeFalse("HWND_NOTOPMOST takes the surface out of the topmost band.");
    }
}
