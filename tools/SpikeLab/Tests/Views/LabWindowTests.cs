using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.Tools.SpikeLab.Reporting;
using Clicalo.Tools.SpikeLab.Session;
using Clicalo.Tools.SpikeLab.Views;

namespace Clicalo.Tools.SpikeLab.Tests.Views;

/// <summary>
/// The control window in process (never shown): it opens centered and never larger than the work area, scrolls with a
/// 44-pixel bar, and opens the reports without typing their path.
/// </summary>
public sealed class LabWindowTests
{
    [Fact]
    public void It_opens_centered_and_never_larger_than_the_work_area() =>
        WpfThread.Invoke(() =>
        {
            var window = Create();
            var area = SystemParameters.WorkArea;

            window.WindowStartupLocation.ShouldBe(WindowStartupLocation.CenterScreen);
            window.Width.ShouldBeLessThanOrEqualTo(area.Width);
            window.Height.ShouldBeLessThanOrEqualTo(area.Height);
            window.MaxWidth.ShouldBe(area.Width);
            window.MaxHeight.ShouldBe(area.Height);
            window
                .Resources[SystemParameters.VerticalScrollBarWidthKey]
                .ShouldBe(LabWindow.ScrollBarWidth);
            LabWindow.ScrollBarWidth.ShouldBeGreaterThanOrEqualTo(44);
        });

    [Fact]
    public void It_has_named_buttons_to_open_the_reports_folder_and_the_summary() =>
        WpfThread.Invoke(() =>
        {
            var buttons = Buttons(Create());

            foreach (var name in (string[])["Abrir carpeta de informes", "Abrir resumen"])
            {
                var button = buttons.Single(button =>
                    string.Equals(
                        AutomationProperties.GetName(button),
                        name,
                        StringComparison.Ordinal
                    )
                );
                button.MinHeight.ShouldBeGreaterThanOrEqualTo(44);
            }
        });

    [Fact]
    public void Its_buttons_never_share_a_name_with_what_a_cycle_uses() =>
        WpfThread.Invoke(() =>
        {
            var names = Buttons(Create()).Select(AutomationProperties.GetName).ToArray();

            names.ShouldContain("Soltar todo desde la ventana de control", StringComparer.Ordinal);
            foreach (
                var used in (string[])
                    [
                        "Soltar todo",
                        "Aviso cortés",
                        "Centro de control",
                        "Forzar activación del panel",
                    ]
            )
            {
                names.ShouldNotContain(name => string.Equals(name, used, StringComparison.Ordinal));
            }
        });

    [Fact]
    public void The_machine_of_the_report_is_read_from_Windows()
    {
        var machine = MachineInfo.Current();

        machine.WindowsBuild.ShouldNotBeNull().ShouldMatch(@"^\d+\.\d+\.\d+\.\d+$");
        machine.Windows.ShouldStartWith(machine.WindowsBuild);
        machine.Monitors.ShouldAllBe(monitor => monitor.Width > 0 && monitor.Dpi >= 96);
        machine.Monitors.Count(monitor => monitor.IsPrimary).ShouldBeLessThanOrEqualTo(1);
    }

    private static LabWindow Create() =>
        new(new LabApp(LabOptions.Default, Dispatcher.CurrentDispatcher), argumentError: null);

    private static List<Button> Buttons(DependencyObject root)
    {
        var buttons = new List<Button>();
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            if (child is Button button)
            {
                buttons.Add(button);
            }
            else
            {
                buttons.AddRange(Buttons(child));
            }
        }

        return buttons;
    }
}
