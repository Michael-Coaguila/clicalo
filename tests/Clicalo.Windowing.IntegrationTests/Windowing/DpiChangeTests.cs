using System.Globalization;
using Clicalo.TestKit.Windows;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.Windowing.IntegrationTests.Desktop;
using Clicalo.Windowing.IntegrationTests.Windowing.Support;

namespace Clicalo.Windowing.IntegrationTests.Windowing;

/// <summary>
/// Spike S1, dotnet/wpf#7561: on <c>WM_DPICHANGED</c> WPF applies the suggested rectangle with <c>SetWindowPos</c>
/// without <c>SWP_NOACTIVATE</c>. A synthetic <c>WM_DPICHANGED</c> with another DPI is sent to the visible panel while
/// InputProbe is in front: the rectangle is applied, WPF rescales, and nothing is activated.
/// </summary>
[Collection(DesktopCollectionDefinition.Name)]
[Trait("Requires", "Desktop")]
[Trait("Req", "REG-01")]
[Trait("Req", "ACC-008")]
public sealed class DpiChangeTests(SurfaceDesktopFixture desktop)
    : IClassFixture<SurfaceDesktopFixture>
{
    private const int Cycles = 20;

    [DesktopFact]
    public async Task A_dpi_change_never_activates_the_surface()
    {
        var panel = desktop.Panel;
        var window = panel.Handle;
        var cursor = await desktop.PrepareAsync();
        var violations = desktop.Lab.Guard.Violations;
        var repairs = desktop.Lab.Integrity.Repairs;
        var monitorDpi = NativeSurface.GetDpiForWindow(window);
        var otherDpi = monitorDpi + (monitorDpi / 2);

        for (var cycle = 1; cycle <= Cycles; cycle++)
        {
            // Odd cycles go to 150 % of the monitor's DPI, even cycles come back, so the panel ends as it started.
            var (from, to) = cycle % 2 == 1 ? (monitorDpi, otherDpi) : (otherDpi, monitorDpi);
            var current = NativeSurface.Bounds(window);
            var suggested = current with
            {
                Right = current.Left + (int)Math.Round(current.Width * (double)to / from),
                Bottom = current.Top + (int)Math.Round(current.Height * (double)to / from),
            };

            // On the panel's own thread, where Windows delivers it.
            WpfThread.Invoke(() =>
                NativeSurface.SendWithStructure(
                    window,
                    NativeSurface.WmDpiChanged,
                    (nint)((to << 16) | to),
                    suggested
                )
            );

            var applied = NativeSurface.Bounds(window);
            (applied.Left, applied.Top, applied.Right, applied.Bottom).ShouldBe(
                (suggested.Left, suggested.Top, suggested.Right, suggested.Bottom)
            );
            ForegroundWindows
                .IsForeground(desktop.Probe.Window)
                .ShouldBeTrue(
                    Say($"The foreground changed after DPI change {cycle}: ")
                        + ForegroundWindows.Describe()
                );
            desktop.Lab.Guard.Violations.ShouldBe(
                violations,
                Say($"DPI change {cycle} activated the panel.")
            );
        }

        WpfThread.Invoke(WpfThread.DrainPendingWork);
        desktop.Lab.Integrity.Repairs.ShouldBe(
            repairs,
            "The checks after each DPI change found nothing to repair."
        );
        panel.Activations.ShouldBeEmpty();
        await desktop.ShouldHaveKeptTheForegroundAsync(cursor);
    }

    private static string Say(FormattableString text) =>
        text.ToString(CultureInfo.InvariantCulture);
}
