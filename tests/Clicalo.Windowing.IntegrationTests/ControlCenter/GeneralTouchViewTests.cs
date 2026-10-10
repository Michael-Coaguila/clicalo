using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Clicalo.Application.Confirmation;
using Clicalo.Domain.Settings;
using Clicalo.Presentation.ControlCenter.General;
using Clicalo.Presentation.ControlCenter.TouchPrecision;
using Clicalo.TestKit;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Workspace.General;
using Clicalo.UI.Wpf.Workspace.TouchPrecision;
using Clicalo.Windowing.IntegrationTests.Theming;

namespace Clicalo.Windowing.IntegrationTests.ControlCenter;

/// <summary>
/// The views of «General y panel» and «Precisión táctil» headless, wide and narrow, in Spanish and English: every
/// button and choice has an accessible name and a touch area of at least 44 × 44 (REG-02, REG-06). With
/// <c>CLICALO_CC_PREVIEW=1</c> it also writes PNG previews to <c>artifacts/cc-preview</c> to compare by eye with the
/// prototype.
/// </summary>
public sealed class GeneralTouchViewTests
{
    private readonly ControlCenterTestWorld _world = new();

    public static TheoryData<string, double> Layouts =>
        new()
        {
            { "es", 1300 },
            { "es", 700 },
            { "en", 1300 },
        };

    [Theory]
    [MemberData(nameof(Layouts))]
    [Trait("Req", "GEN-001")]
    [Trait("Req", "REG-02")]
    [Trait("Req", "REG-06")]
    [Trait("Req", "CCM-005")]
    public void The_general_section_names_every_control_and_keeps_the_touch_size(
        string language,
        double width
    )
    {
        _world.Localization.TrySetLanguage(language).ShouldBeTrue();
        var section = new GeneralSectionViewModel(
            new GeneralServices(
                _world.Store,
                _world.Localization,
                new TwoStepConfirm(_world.Time),
                _world.Time,
                action => action(),
                () => { }
            )
        );

        Check(
            () => new GeneralSectionView(section),
            width,
            3000,
            "general-" + language + "-" + width
        );
    }

    [Theory]
    [MemberData(nameof(Layouts))]
    [Trait("Req", "TAC-005")]
    [Trait("Req", "TAC-006")]
    [Trait("Req", "REG-02")]
    [Trait("Req", "REG-06")]
    public void The_touch_section_names_every_control_and_keeps_the_touch_size(
        string language,
        double width
    )
    {
        _world.Localization.TrySetLanguage(language).ShouldBeTrue();
        var section = new TouchPrecisionViewModel(
            new TouchPrecisionServices(
                _world.Store,
                _world.Localization,
                _world.Time,
                action => action()
            )
        );

        Check(
            () => new TouchPrecisionView(section),
            width,
            width < 1240 ? 1500 : 700,
            "touch-" + language + "-" + width
        );
    }

    private static void Check(
        Func<FrameworkElement> create,
        double width,
        double height,
        string name
    )
    {
        WpfThread.Invoke(() =>
        {
            using var theme = new ThemeService(
                new FakeSystemTheme(),
                ThemeChoice.Dark,
                100,
                reduceMotion: true
            );
            var view = create();
            theme.Attach(view);
            view.Width = width;
            view.Measure(new Size(width, height));
            view.Arrange(new Rect(0, 0, width, height));
            view.UpdateLayout();
            var controls = Descendants(view)
                .OfType<ButtonBase>()
                .Where(static b => b is not RepeatButton && b.IsHitTestVisible)
                .ToList();
            controls.Count.ShouldBeGreaterThan(10);
            foreach (var control in controls)
            {
                AutomationProperties
                    .GetName(control)
                    .ShouldNotBeNullOrWhiteSpace(control.ToString());
                control.ActualHeight.ShouldBeGreaterThanOrEqualTo(
                    44,
                    AutomationProperties.GetName(control)
                );
                control.ActualWidth.ShouldBeGreaterThanOrEqualTo(
                    44,
                    AutomationProperties.GetName(control)
                );
            }

            theme.Detach(view);
        });

        if (
            !string.Equals(
                Environment.GetEnvironmentVariable("CLICALO_CC_PREVIEW"),
                "1",
                StringComparison.Ordinal
            )
        )
        {
            return;
        }

        ThemeService? previewTheme = null;
        FrameworkElement? root = null;
        var png = RenderSnapshot.Render(
            () =>
            {
                previewTheme = new ThemeService(
                    new FakeSystemTheme(),
                    ThemeChoice.Dark,
                    100,
                    reduceMotion: true
                );
                root = create();
                previewTheme.Attach(root);
                return root;
            },
            new RenderSnapshotOptions { Width = width, Height = height }
        );
        WpfThread.Invoke(() =>
        {
            previewTheme!.Detach(root!);
            previewTheme.Dispose();
        });
        var folder = Path.Combine(RepoPaths.Root, "artifacts", "cc-preview");
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, name + ".png"), png);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var nested in Descendants(child))
            {
                yield return nested;
            }
        }
    }
}
