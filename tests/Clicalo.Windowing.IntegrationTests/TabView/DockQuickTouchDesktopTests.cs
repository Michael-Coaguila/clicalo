using System.Collections.Immutable;
using System.Windows;
using System.Windows.Media;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.Settings;
using Clicalo.Domain.Touch;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Surfaces.TabView;
using Clicalo.UI.Wpf.Theming;
using Clicalo.Windowing.IntegrationTests.Desktop;
using Clicalo.Windowing.IntegrationTests.Interactions;
using Clicalo.Windowing.IntegrationTests.MinimalPanel;
using Clicalo.Windowing.IntegrationTests.Theming;
using Clicalo.Windowing.IntegrationTests.Windowing.Support;
using Microsoft.Extensions.Time.Testing;

namespace Clicalo.Windowing.IntegrationTests.TabView;

/// <summary>
/// Quick settings beside the bar of the Tab view, on a real desktop, reached the way a finger reaches it: frames of the
/// pointer layer on the center of an option, still or with the small shake of a finger, choose it (PES-009, AJR-001).
/// </summary>
[Collection(DesktopCollectionDefinition.Name)]
[Trait("Requires", "Desktop")]
public sealed class DockQuickTouchDesktopTests
{
    [DesktopFact]
    [Trait("Req", "PES-009")]
    [Trait("Req", "AJR-001")]
    [Trait("Req", "TAC-004")]
    public void A_tap_on_Completa_beside_the_bar_leaves_the_Tab_view()
    {
        foreach (var shake in new[] { 0, 4 })
        {
            using var lab = SurfaceLab.Create();
            var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var (quick, world, theme) = WpfThread.Invoke(() =>
            {
                var theme = new ThemeService(
                    new FakeSystemTheme(),
                    ThemeChoice.Dark,
                    100,
                    reduceMotion: true
                );
                var world = new InteractionsWorld();
                world.QuickSettings.Views[2].Select();
                world.QuickSettings.Open();
                var quick = new DockQuickWindow(
                    world.QuickSettings,
                    lab.Registry,
                    time,
                    theme,
                    PanelDesktopFixture.Touch
                );
                var scale = DisplayMonitorsScale();
                quick.FitHeight(560);
                var (width, height) = quick.MeasurePhysical(scale);
                quick.PresentAt(new PhysicalRect(100, 100, width, height));
                return (quick, world, theme);
            });
            try
            {
                WpfThread.Invoke(WpfThread.DrainPendingWork);
                world.Settings.Density.ShouldBe(PanelDensity.Dock);
                var center = WpfThread.Invoke(() =>
                {
                    var option = Named(quick, "Completa");
                    var point = option.PointToScreen(
                        new Point(option.ActualWidth / 2, option.ActualHeight / 2)
                    );
                    return new PhysicalPoint((int)point.X, (int)point.Y);
                });

                Feed(quick, time, PointerPhase.Down, center);
                time.Advance(TimeSpan.FromMilliseconds(60));
                Feed(quick, time, PointerPhase.Move, center with { X = center.X + shake });
                time.Advance(TimeSpan.FromMilliseconds(60));
                Feed(quick, time, PointerPhase.Up, center with { X = center.X + shake });
                time.Advance(TimeSpan.FromMilliseconds(500));
                WpfThread.Invoke(WpfThread.DrainPendingWork);

                world.Settings.Density.ShouldBe(
                    PanelDensity.Full,
                    FormattableString.Invariant($"a shake of {shake} px")
                );
            }
            finally
            {
                WpfThread.Invoke(() =>
                {
                    quick.Close();
                    theme.Dispose();
                });
            }
        }
    }

    private static double DisplayMonitorsScale() =>
        Clicalo.UI.Wpf.Windowing.DisplayMonitors.Snapshot()[0].Scale;

    private static void Feed(
        DockQuickWindow quick,
        FakeTimeProvider time,
        PointerPhase phase,
        PhysicalPoint at
    )
    {
        var now = time.GetUtcNow();
        ImmutableArray<PointerSample> samples =
        [
            new PointerSample(
                1,
                PointerKind.Finger,
                phase,
                at,
                new PhysicalRect(at.X - 5, at.Y - 5, 10, 10),
                now,
                PointerInputOrigin.Injected
            ),
        ];
        WpfThread.Invoke(() => quick.OnFrame(new PointerFrame(1, now, samples)));
    }

    private static FrameworkElement Named(DependencyObject parent, string name)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (
                child is FrameworkElement element
                && string.Equals(
                    System.Windows.Automation.AutomationProperties.GetName(element),
                    name,
                    StringComparison.Ordinal
                )
            )
            {
                return element;
            }

            if (TryNamed(child, name) is { } found)
            {
                return found;
            }
        }

        throw new InvalidOperationException("No element named " + name);
    }

    private static FrameworkElement? TryNamed(DependencyObject parent, string name)
    {
        try
        {
            return Named(parent, name);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
