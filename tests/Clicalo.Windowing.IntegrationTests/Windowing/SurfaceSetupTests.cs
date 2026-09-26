using Clicalo.Application.Ports;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Windowing;
using Clicalo.Windowing.IntegrationTests.Windowing.Support;

namespace Clicalo.Windowing.IntegrationTests.Windowing;

/// <summary>
/// What <see cref="NonActivatingWindow"/> applies before any show (blueprint §3.5) and how <see cref="SurfaceRegistry"/>
/// tracks the surfaces. Headless: the surfaces get a handle but are never shown, so nothing appears on screen and the
/// foreground is never touched.
/// </summary>
public sealed class SurfaceSetupTests
{
    [Fact]
    [Trait("Req", "REG-01")]
    public void A_surface_is_non_activatable_topmost_and_owned_before_it_is_ever_shown()
    {
        using var lab = SurfaceLab.Create();
        var surface = SurfaceLab.WithHandle(lab.CreateSurface(SurfaceKind.Panel, 0, 200, 100));
        var window = surface.Handle;

        NativeSurface.IsWindowVisible(window).ShouldBeFalse();
        NativeSurface
            .HasExStyle(window, NativeSurface.ExNoActivate | NativeSurface.ExTopmost)
            .ShouldBeTrue("WS_EX_NOACTIVATE | WS_EX_TOPMOST are applied in OnSourceInitialized.");
        (
            NativeSurface.ExStyle(window) & (NativeSurface.ExAppWindow | NativeSurface.ExToolWindow)
        ).ShouldBe(
            0u,
            "Out of Alt+Tab through the owner anchor, not WS_EX_TOOLWINDOW; never WS_EX_APPWINDOW."
        );
        NativeSurface
            .GetWindow(window, NativeSurface.Owner)
            .ShouldBe(lab.Anchor.Window.Handle, "The hidden OwnerAnchor owns every surface.");
    }

    [Fact]
    [Trait("Req", "REG-01")]
    public void A_surface_is_in_the_registry_while_its_handle_lives()
    {
        using var lab = SurfaceLab.Create();
        var surface = lab.CreateSurface(SurfaceKind.Bubble, 0, 64, 64);
        surface.SurfaceWindow.IsNone.ShouldBeTrue("No handle before it is needed.");
        lab.Registry.Surfaces.ShouldBeEmpty();

        SurfaceLab.WithHandle(surface);
        var window = surface.SurfaceWindow;

        lab.Registry.Surfaces.Count(registered => ReferenceEquals(registered, surface)).ShouldBe(1);
        lab.Registry.TryGetSurface(window, out var id).ShouldBeTrue();
        id.ShouldBe(new SurfaceId(SurfaceKind.Bubble, 0));

        lab.Close(surface);

        lab.Registry.Surfaces.ShouldBeEmpty();
        lab.Registry.TryGetSurface(window, out _).ShouldBeFalse();
        surface.SurfaceWindow.IsNone.ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "REG-01")]
    public void Two_live_surfaces_cannot_share_an_id()
    {
        using var lab = SurfaceLab.Create();
        SurfaceLab.WithHandle(lab.CreateSurface(SurfaceKind.Menu, 3, 120, 80));
        var twin = lab.CreateSurface(SurfaceKind.Menu, 3, 120, 80);

        Should.Throw<InvalidOperationException>(() => SurfaceLab.WithHandle(twin));

        lab.Registry.Surfaces.Length.ShouldBe(1);
        twin.SurfaceWindow.IsNone.ShouldBeTrue(
            "A surface that could not register is not a live surface."
        );
    }

    [Fact]
    [Trait("Req", "REG-01")]
    [Trait("Req", "BUS-002")]
    public void A_lease_removes_WS_EX_NOACTIVATE_from_any_thread_until_it_ends()
    {
        using var lab = SurfaceLab.Create();
        var surface = SurfaceLab.WithHandle(lab.CreateSurface(SurfaceKind.Panel, 0, 200, 100));

        // Called from the test thread, as the orchestrator calls it from SysEvents.
        lab.Registry.AllowActivation(surface.Id);

        lab.Registry.IsActivationAllowed(surface.Id).ShouldBeTrue();
        NativeSurface.HasExStyle(surface.Handle, NativeSurface.ExNoActivate).ShouldBeFalse();

        lab.Registry.RestoreNoActivate(surface.Id);

        lab.Registry.IsActivationAllowed(surface.Id).ShouldBeFalse();
        NativeSurface.HasExStyle(surface.Handle, NativeSurface.ExNoActivate).ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "REG-01")]
    public void Unknown_surfaces_are_ignored_by_the_lease_style()
    {
        using var lab = SurfaceLab.Create();
        var unknown = new SurfaceId(SurfaceKind.Notice, 9);

        lab.Registry.AllowActivation(unknown);

        lab.Registry.IsActivationAllowed(unknown).ShouldBeFalse();
        lab.Registry.TryGetSurface(new WindowToken(0x1234), out _).ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "REG-01")]
    [Trait("Req", "ACC-008")]
    public void A_passive_move_lands_on_the_physical_rectangle_without_showing()
    {
        using var lab = SurfaceLab.Create();
        var surface = lab.CreateSurface(SurfaceKind.SideWindow, 0, 240, 240);
        var (work, _) = NativeSurface.PrimaryWorkArea();
        var target = new Clicalo.Domain.Geometry.PhysicalRect(
            work.Left + 10,
            work.Top + 20,
            300,
            200
        );

        WpfThread.Invoke(() => surface.MovePassive(target));

        var bounds = NativeSurface.Bounds(surface.Handle);
        (bounds.Left, bounds.Top, bounds.Width, bounds.Height).ShouldBe(
            (target.Left, target.Top, 300, 200)
        );
        NativeSurface.IsWindowVisible(surface.Handle).ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "REG-01")]
    public void Hiding_a_surface_that_was_never_created_creates_nothing()
    {
        using var lab = SurfaceLab.Create();
        var surface = lab.CreateSurface(SurfaceKind.Notice, 0, 200, 60);

        WpfThread.Invoke(surface.HidePassive);

        surface.SurfaceWindow.IsNone.ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "REG-01")]
    public void The_owner_anchor_is_a_hidden_window_created_once()
    {
        var (before, created, again, visible, after) = WpfThread.Invoke(() =>
        {
            var anchor = new OwnerAnchor();
            var none = anchor.Window;
            var first = anchor.EnsureCreated();
            var second = anchor.EnsureCreated();
            var shown = NativeSurface.IsWindowVisible(first.Handle);
            anchor.Dispose();
            return (none, first, second, shown, anchor.Window);
        });

        before.IsNone.ShouldBeTrue();
        created.IsNone.ShouldBeFalse();
        again.ShouldBe(created);
        visible.ShouldBeFalse();
        after.IsNone.ShouldBeTrue();
        NativeSurface.IsWindow(created.Handle).ShouldBeFalse("Dispose destroys the anchor window.");
    }
}
