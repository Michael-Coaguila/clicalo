using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Clicalo.Application.Ports;
using Clicalo.Domain.Dimming;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.Settings;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Windowing;
using Clicalo.Windowing.IntegrationTests.Windowing.Support;

namespace Clicalo.Windowing.IntegrationTests.Windowing;

/// <summary>
/// Spike S6, the deterministic half: the decision on transparency (a per-pixel transparent surface with rounded corners
/// and its shadow in a separate click-through window), the shadow raster and the opacity. Headless: the surfaces get a
/// handle but are never shown. What hit testing and DWM do with them is <see cref="SurfaceLookDesktopTests"/>.
/// </summary>
public sealed class SurfaceLookTests
{
    /// <summary>The maintainer's screen: 2400 × 1600 at 175 %.</summary>
    private const double Scale = 1.75;

    private const uint ExLayered = 0x0008_0000;
    private const uint ExTransparent = 0x0000_0020;

    private static readonly Color Shadow = Color.FromArgb(115, 0, 0, 0);

    [Fact]
    [Trait("Req", "PAN-003")]
    public void The_panel_shadow_reaches_as_far_as_its_css_box_shadow()
    {
        var shadow = ShadowRaster.Render(700, 900, Scale, SurfaceLook.Panel, Shadow);

        // blur 50 → 88 px on every side; offset 18 → 32 px down.
        (shadow.Left, shadow.Top, shadow.Right, shadow.Bottom).ShouldBe((88, 56, 88, 120));
        shadow.Bitmap.PixelWidth.ShouldBe(700 + 88 + 88);
        shadow.Bitmap.PixelHeight.ShouldBe(900 + 56 + 120);
        shadow.Bitmap.Format.ShouldBe(PixelFormats.Pbgra32);
        shadow.Bitmap.IsFrozen.ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "PAN-003")]
    public void The_shadow_is_never_under_the_surface_so_a_translucent_panel_does_not_show_it()
    {
        var shadow = ShadowRaster.Render(700, 900, Scale, SurfaceLook.Panel, Shadow);
        var alpha = Alphas(shadow.Bitmap);
        var radius = (int)Math.Ceiling(18 * Scale);

        for (var y = shadow.Top; y < shadow.Top + 900; y += 7)
        {
            for (var x = shadow.Left; x < shadow.Left + 700; x += 7)
            {
                var inCorner =
                    (x - shadow.Left < radius || shadow.Left + 700 - x <= radius)
                    && (y - shadow.Top < radius || shadow.Top + 900 - y <= radius);
                if (!inCorner)
                {
                    alpha(x, y)
                        .ShouldBe(
                            (byte)0,
                            string.Create(
                                CultureInfo.InvariantCulture,
                                $"({x}, {y}) is under the surface."
                            )
                        );
                }
            }
        }
    }

    [Fact]
    [Trait("Req", "PAN-003")]
    public void The_shadow_falls_below_the_surface_and_fades_out_at_its_edge()
    {
        var shadow = ShadowRaster.Render(700, 900, Scale, SurfaceLook.Panel, Shadow);
        var alpha = Alphas(shadow.Bitmap);
        var centerX = shadow.Left + 350;
        var below = alpha(centerX, shadow.Top + 900 + 1);
        var above = alpha(centerX, shadow.Top - 2);

        below.ShouldBeGreaterThan((byte)60, "Right below the panel the shadow is dark.");
        below.ShouldBeLessThanOrEqualTo(Shadow.A);
        above.ShouldBeLessThan(below, "The offset moves the shadow down.");
        alpha(centerX, shadow.Bitmap.PixelHeight - 1).ShouldBeLessThanOrEqualTo((byte)4);
        alpha(0, shadow.Top + 450).ShouldBeLessThanOrEqualTo((byte)4);
    }

    [Fact]
    [Trait("Req", "PAN-003")]
    [Trait("Req", "BUR-001")]
    public void Corners_are_the_radii_of_the_prototype_in_physical_pixels()
    {
        ShadowRaster.Corners(SurfaceLook.Panel, 700, 900, Scale).ShouldBe(new CornerRadius(31.5));
        ShadowRaster.Corners(SurfaceLook.Bubble, 112, 112, Scale).ShouldBe(new CornerRadius(56));
        ShadowRaster
            .Corners(SurfaceLook.DockHandle(DockSide.Right), 84, 200, Scale)
            .ShouldBe(new CornerRadius(21, 0, 0, 21));
        ShadowRaster
            .Corners(SurfaceLook.DockHandle(DockSide.Bottom), 200, 84, Scale)
            .ShouldBe(new CornerRadius(21, 21, 0, 0));
        ShadowRaster
            .Corners(SurfaceLook.Panel, 40, 20, Scale)
            .ShouldBe(new CornerRadius(10), "Never more than half a side.");
    }

    [Fact]
    [Trait("Req", "PAN-003")]
    [Trait("Req", "REG-01")]
    public void A_surface_with_a_look_is_per_pixel_transparent_and_never_activates()
    {
        using var lab = SurfaceLab.Create();
        var surface = SurfaceLab.WithHandle(
            lab.CreateSurface(SurfaceKind.Panel, 0, 200, 100, SurfaceLook.Panel)
        );

        NativeSurface
            .HasExStyle(
                surface.Handle,
                ExLayered | NativeSurface.ExNoActivate | NativeSurface.ExTopmost
            )
            .ShouldBeTrue();
        WpfThread.Invoke(() => surface.AllowsTransparency).ShouldBeTrue();
        surface.ShadowWindow.ShouldNotBe(WindowToken.None);
    }

    [Fact]
    [Trait("Req", "PAN-003")]
    [Trait("Req", "REG-01")]
    public void The_shadow_is_a_click_through_window_that_follows_the_surface()
    {
        using var lab = SurfaceLab.Create();
        var surface = lab.CreateSurface(SurfaceKind.Panel, 0, 200, 100, SurfaceLook.Panel);
        var (work, _) = NativeSurface.PrimaryWorkArea();
        WpfThread.Invoke(() =>
            surface.MovePassive(new PhysicalRect(work.Left + 120, work.Top + 120, 400, 300))
        );
        var shadow = surface.ShadowWindow.Handle;

        NativeSurface
            .HasExStyle(
                shadow,
                ExLayered
                    | ExTransparent
                    | NativeSurface.ExNoActivate
                    | NativeSurface.ExTopmost
                    | NativeSurface.ExToolWindow
            )
            .ShouldBeTrue("WS_EX_TRANSPARENT on a layered window: every touch goes through.");
        NativeSurface.GetWindow(shadow, NativeSurface.Owner).ShouldBe(lab.Anchor.Window.Handle);
        lab.Registry.TryGetSurface(new WindowToken(shadow), out _)
            .ShouldBeFalse("It is not a surface.");

        var image = WpfThread.Invoke(() => surface.Shadow).ShouldNotBeNull();
        var bounds = NativeSurface.Bounds(shadow);
        (bounds.Left, bounds.Top, bounds.Right, bounds.Bottom).ShouldBe(
            (
                work.Left + 120 - image.Left,
                work.Top + 120 - image.Top,
                work.Left + 120 + 400 + image.Right,
                work.Top + 120 + 300 + image.Bottom
            )
        );
        NativeSurface.IsWindowVisible(shadow).ShouldBeFalse("Hidden like its surface.");

        lab.Close(surface);
        NativeSurface.IsWindow(shadow).ShouldBeFalse("It goes with its surface.");
    }

    [Fact]
    [Trait("Req", "PAN-003")]
    public void A_surface_without_a_look_stays_an_opaque_window_without_a_shadow()
    {
        using var lab = SurfaceLab.Create();
        var surface = SurfaceLab.WithHandle(lab.CreateSurface(SurfaceKind.Notice, 0, 200, 100));

        NativeSurface.HasExStyle(surface.Handle, ExLayered).ShouldBeFalse();
        surface.ShadowWindow.ShouldBe(WindowToken.None);
        WpfThread.Invoke(() => surface.Shadow).ShouldBeNull();
    }

    [Fact]
    public void The_look_is_fixed_once_the_handle_exists()
    {
        using var lab = SurfaceLab.Create();
        var surface = SurfaceLab.WithHandle(lab.CreateSurface(SurfaceKind.Panel, 0, 200, 100));

        Should.Throw<InvalidOperationException>(() =>
            WpfThread.Invoke(() => surface.ChangeLook(SurfaceLook.Panel))
        );
    }

    [Fact]
    [Trait("Req", "GEN-009")]
    [Trait("Req", "TEM-006")]
    public void The_opacity_changes_at_once_with_reduce_motion_and_over_350_ms_without_it()
    {
        using var lab = SurfaceLab.Create();
        var surface = lab.CreateSurface(SurfaceKind.Panel, 0, 200, 100, SurfaceLook.Panel);
        var left = new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
        var dimmed = DimPolicy.Evaluate(
            new DimInputs(
                AutoDim: true,
                Opacity: 0.92,
                DimTo: 0.35,
                DimSurface.Panel,
                PointerInside: false,
                left,
                DimExceptions.None,
                ReduceMotion: true,
                HighContrast: false,
                left + TimeSpan.FromSeconds(3)
            )
        );

        WpfThread.Invoke(() =>
        {
            surface.ApplyDim(dimmed);
            surface.Opacity.ShouldBe(0.35);
            surface.HasAnimatedProperties.ShouldBeFalse();

            surface.ApplyDim(
                dimmed with
                {
                    TargetOpacity = 0.92,
                    Transition = TimeSpan.FromMilliseconds(350),
                }
            );
            surface.HasAnimatedProperties.ShouldBeTrue("A 350 ms fade.");

            surface.FadeTo(1, TimeSpan.Zero);
            surface.Opacity.ShouldBe(1);
            surface.HasAnimatedProperties.ShouldBeFalse("A change at once stops the running fade.");
        });
        Should.Throw<ArgumentOutOfRangeException>(() =>
            WpfThread.Invoke(() => surface.FadeTo(1.2, TimeSpan.Zero))
        );
    }

    /// <summary>The alpha of each pixel of <paramref name="bitmap"/>.</summary>
    private static Func<int, int, byte> Alphas(BitmapSource bitmap)
    {
        var stride = bitmap.PixelWidth * 4;
        var pixels = new byte[stride * bitmap.PixelHeight];
        bitmap.CopyPixels(pixels, stride, 0);
        return (x, y) => pixels[(y * stride) + (x * 4) + 3];
    }
}
