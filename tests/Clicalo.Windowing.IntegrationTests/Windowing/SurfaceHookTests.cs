using Clicalo.Application.Ports;
using Clicalo.Domain.Geometry;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.Windowing.IntegrationTests.Windowing.Support;

namespace Clicalo.Windowing.IntegrationTests.Windowing;

/// <summary>
/// The common <c>HwndSource</c> hook of the surfaces, message by message (blueprint §3.5, hook table). Headless: the
/// messages are sent to surfaces that have a handle but are never shown.
/// </summary>
public sealed class SurfaceHookTests
{
    private const uint Dpi144 = 144;

    [Fact]
    [Trait("Req", "REG-01")]
    public void A_click_or_a_touch_never_activates_a_surface()
    {
        using var lab = SurfaceLab.Create();
        var window = SurfaceLab
            .WithHandle(lab.CreateSurface(SurfaceKind.Panel, 0, 200, 100))
            .Handle;

        // Sent from the surface's own thread: Windows drops WM_POINTER* messages sent from another thread.
        var (mouse, pointer) = WpfThread.Invoke(() =>
            (
                NativeSurface.SendMessageW(window, NativeSurface.WmMouseActivate, window, 0),
                NativeSurface.SendMessageW(window, NativeSurface.WmPointerActivate, 0, 0)
            )
        );

        mouse.ShouldBe(NativeSurface.MouseNoActivate, "WM_MOUSEACTIVATE answers MA_NOACTIVATE.");
        pointer.ShouldBe(
            NativeSurface.PointerNoActivate,
            "WM_POINTERACTIVATE answers PA_NOACTIVATE."
        );
    }

    [Fact]
    [Trait("Req", "REG-01")]
    [Trait("Req", "BUS-002")]
    public void Every_position_change_gets_SWP_NOACTIVATE_except_under_a_lease()
    {
        using var lab = SurfaceLab.Create();
        var surface = SurfaceLab.WithHandle(lab.CreateSurface(SurfaceKind.Dock, 0, 48, 240));

        Flags(surface).ShouldBe(Requested | NativeSurface.NoActivate);

        lab.Registry.AllowActivation(surface.Id);
        Flags(surface)
            .ShouldBe(
                Requested,
                "A TextInput or KeyboardNavigation lease may activate the surface."
            );

        lab.Registry.RestoreNoActivate(surface.Id);
        Flags(surface).ShouldBe(Requested | NativeSurface.NoActivate);

        static uint Flags(TestSurface surface)
        {
            var position = new NativeSurface.WindowPosition
            {
                Window = surface.Handle,
                Flags = Requested,
            };
            return NativeSurface
                .SendWithStructure(surface.Handle, NativeSurface.WmWindowPosChanging, 0, position)
                .After.Flags;
        }
    }

    [Fact]
    [Trait("Req", "ACC-008")]
    public void A_new_dpi_keeps_the_logical_size_of_the_surface()
    {
        using var lab = SurfaceLab.Create();
        var window = SurfaceLab
            .WithHandle(lab.CreateSurface(SurfaceKind.Panel, 0, 200, 100))
            .Handle;

        var (handled, size) = NativeSurface.SendWithStructure(
            window,
            NativeSurface.WmGetDpiScaledSize,
            (nint)Dpi144,
            new NativeSurface.Size()
        );

        handled.ShouldBe(1, "WM_GETDPISCALEDSIZE is answered with the surface's own size.");
        (size.Width, size.Height).ShouldBe((300, 150), "200 × 100 logical units at 150 %.");
    }

    [Fact]
    [Trait("Req", "REG-01")]
    [Trait("Req", "ACC-008")]
    public void A_dpi_change_applies_the_suggested_rectangle_without_activating()
    {
        using var failures = DebugFailures.Capture();
        using var lab = SurfaceLab.Create();
        var surface = lab.CreateSurface(SurfaceKind.Panel, 0, 200, 100);
        var (work, _) = NativeSurface.PrimaryWorkArea();
        WpfThread.Invoke(() =>
            surface.MovePassive(new PhysicalRect(work.Left + 40, work.Top + 40, 200, 100))
        );
        var window = surface.Handle;
        var dpi = NativeSurface.GetDpiForWindow(window);
        var newDpi = dpi + (dpi / 2);
        var suggested = new NativeSurface.Rect
        {
            Left = work.Left + 50,
            Top = work.Top + 60,
            Right = work.Left + 50 + 300,
            Bottom = work.Top + 60 + 150,
        };

        // WPF answers with SetWindowPos(SWP_NOZORDER | SWP_ASYNCWINDOWPOS), without SWP_NOACTIVATE (#7561).
        WpfThread.Invoke(() =>
            NativeSurface.SendWithStructure(
                window,
                NativeSurface.WmDpiChanged,
                (nint)((newDpi << 16) | newDpi),
                suggested
            )
        );

        var bounds = NativeSurface.Bounds(window);
        (bounds.Left, bounds.Top, bounds.Right, bounds.Bottom).ShouldBe(
            (suggested.Left, suggested.Top, suggested.Right, suggested.Bottom)
        );
        NativeSurface.IsWindowVisible(window).ShouldBeFalse();
        surface.Activations.ShouldBeEmpty("WPF's SetWindowPos ran inside the activation veto.");
        lab.Guard.Violations.ShouldBe(0);
        failures.Messages.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "REG-01")]
    public void The_shadow_margin_is_never_the_surface()
    {
        using var lab = SurfaceLab.Create();
        var surface = lab.CreateSurface(SurfaceKind.Panel, 0, 200, 100);
        var (work, _) = NativeSurface.PrimaryWorkArea();
        WpfThread.Invoke(() =>
        {
            surface.UseShadowMargin(10);
            surface.MovePassive(new PhysicalRect(work.Left + 40, work.Top + 40, 400, 300));
        });
        var window = surface.Handle;
        var margin = (int)Math.Round(10 * NativeSurface.GetDpiForWindow(window) / 96.0);
        var bounds = NativeSurface.Bounds(window);

        HitTest(bounds.Left + 1, bounds.Top + 1)
            .ShouldBe(NativeSurface.HitNowhere, "Top-left corner, inside the margin.");
        HitTest(bounds.Right - margin, bounds.CenterY)
            .ShouldBe(NativeSurface.HitNowhere, "Right edge of the margin.");
        HitTest(bounds.Left + margin, bounds.Top + margin)
            .ShouldBe(NativeSurface.HitClient, "First pixel of the content.");
        HitTest(bounds.CenterX, bounds.CenterY)
            .ShouldBe(NativeSurface.HitClient, "Center of the content.");

        nint HitTest(int x, int y) =>
            NativeSurface.SendMessageW(
                window,
                NativeSurface.WmNcHitTest,
                0,
                NativeSurface.PointParameter(x, y)
            );
    }

    private const uint Requested =
        NativeSurface.NoMove | NativeSurface.NoSize | NativeSurface.NoZOrder;
}
