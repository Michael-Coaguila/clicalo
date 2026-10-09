using System.Diagnostics;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Media;
using Clicalo.Application.Ports;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.Timing;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Windowing;
using Clicalo.Windowing.IntegrationTests.Desktop;
using Clicalo.Windowing.IntegrationTests.Windowing.Support;

namespace Clicalo.Windowing.IntegrationTests.Windowing;

/// <summary>
/// Spike S6 on a real desktop: what hit testing and DWM do with a surface that has a <see cref="SurfaceLook"/>. A green
/// backdrop surface of this test sits under a red panel; nothing is injected: <c>WindowFromPoint</c> says who a touch
/// would reach and the screen says what DWM composed. Only this process's windows are read.
/// </summary>
[Collection(DesktopCollectionDefinition.Name)]
[Trait("Requires", "Desktop")]
public sealed class SurfaceLookDesktopTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(3);

    [DesktopFact]
    [Trait("Req", "PAN-003")]
    [Trait("Req", "REG-01")]
    public async Task The_shadow_and_the_rounded_corners_let_every_touch_through()
    {
        using var lab = SurfaceLab.Create();
        var (backdrop, panel) = await ShowAsync(lab);
        var b = NativeSurface.Bounds(panel.Handle);
        var shadow = panel.ShadowWindow.Handle;

        ScreenPoints.WindowAt(b.CenterX, b.CenterY).ShouldBe(panel.Handle, "The panel itself.");
        ScreenPoints
            .WindowAt(b.Left + 1, b.Top + 1)
            .ShouldBe(backdrop.Handle, "A rounded corner lets the touch through.");
        foreach (var below in new[] { 2, 12, 40 })
        {
            ScreenPoints
                .WindowAt(b.CenterX, b.Bottom + below)
                .ShouldBe(
                    backdrop.Handle,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"The shadow {below} px below the panel lets the touch through."
                    )
                );
        }

        ScreenPoints
            .WindowAt(b.Left - 6, b.CenterY)
            .ShouldBe(backdrop.Handle, "The shadow beside the panel lets the touch through.");
        await Eventually(
            () => ScreenPoints.ColorAt(b.CenterX, b.Bottom + 6).G < 200,
            "The shadow is drawn below the panel."
        );
        NativeSurface
            .GetWindow(panel.Handle, NativeSurface.NextInZOrder)
            .ShouldBe(shadow, "The shadow is right below its surface.");

        WpfThread.Invoke(panel.HidePassive);
        NativeSurface.IsWindowVisible(shadow).ShouldBeFalse("It hides with its surface.");
        lab.Guard.Violations.ShouldBe(0);
    }

    [DesktopFact]
    [Trait("Req", "GEN-009")]
    public async Task The_opacity_shows_what_is_behind_the_panel()
    {
        using var lab = SurfaceLab.Create();
        var (_, panel) = await ShowAsync(lab);
        var b = NativeSurface.Bounds(panel.Handle);

        WpfThread.Invoke(() => panel.FadeTo(0.5, TimeSpan.Zero));
        await ComposedFrame.WaitAsync(panel, TestContext.Current.CancellationToken);

        await Eventually(
            () =>
                ScreenPoints.ColorAt(b.CenterX, b.CenterY)
                    is { R: > 110 and < 145, G: > 110 and < 145 },
            "Half red, half the green behind."
        );
        lab.Guard.Violations.ShouldBe(0);
    }

    [DesktopFact]
    [Trait("Category", "Perf")]
    [Trait("Req", "GEN-009")]
    public async Task A_dim_fades_at_60_frames_per_second()
    {
        using var lab = SurfaceLab.Create();
        var (_, panel) = await ShowAsync(lab);
        var frames = new List<double>();
        var clock = Stopwatch.StartNew();
        void OnFrame(object? sender, EventArgs e) => frames.Add(clock.Elapsed.TotalMilliseconds);

        WpfThread.Invoke(() =>
        {
            CompositionTarget.Rendering += OnFrame;
            clock.Restart();
            panel.FadeTo(0.35, Timings.Dimming.DimTransition);
        });
        await Task.Delay(Timings.Dimming.DimTransition, TestContext.Current.CancellationToken);
        WpfThread.Invoke(() => CompositionTarget.Rendering -= OnFrame);

        var gaps = WpfThread.Invoke(() =>
            frames.Zip(frames.Skip(1), (a, b) => b - a).Order().ToList()
        );
        gaps.Count.ShouldBeGreaterThanOrEqualTo(18, "60 fps over 350 ms is 21 frames.");
        gaps[(int)(gaps.Count * 0.95)]
            .ShouldBeLessThanOrEqualTo(34, "No frame of the fade is lost twice.");
    }

    /// <summary>A 800 × 700 green backdrop and a 400 × 300 red panel with its look over it, both shown passively.</summary>
    private static async Task<(TestSurface Backdrop, TestSurface Panel)> ShowAsync(SurfaceLab lab)
    {
        var backdrop = lab.CreateSurface(SurfaceKind.Notice, 9, 10, 10);
        var panel = lab.CreateSurface(SurfaceKind.Panel, 0, 10, 10, SurfaceLook.Panel);
        var (work, _) = NativeSurface.PrimaryWorkArea();
        WpfThread.Invoke(() =>
        {
            backdrop.Content = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0, 255, 0)),
            };
            panel.Content = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(255, 0, 0)),
            };
            backdrop.MovePassive(new PhysicalRect(work.Left + 40, work.Top + 40, 800, 700));
            panel.MovePassive(new PhysicalRect(work.Left + 240, work.Top + 160, 400, 300));
            backdrop.ShowPassive();
            panel.ShowPassive();
        });
        await ComposedFrame.WaitAsync(backdrop, TestContext.Current.CancellationToken);
        await ComposedFrame.WaitAsync(panel, TestContext.Current.CancellationToken);
        return (backdrop, panel);
    }

    private static async Task Eventually(Func<bool> condition, string what)
    {
        var clock = Stopwatch.StartNew();
        while (!condition())
        {
            clock.Elapsed.ShouldBeLessThan(Patience, what);
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }
    }
}
