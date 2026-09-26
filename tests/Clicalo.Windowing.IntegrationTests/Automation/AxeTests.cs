using System.Text;
using Axe.Windows.Automation;
using Axe.Windows.Automation.Data;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.Windowing.IntegrationTests.Automation.Lab;
using Clicalo.Windowing.IntegrationTests.Desktop;

namespace Clicalo.Windowing.IntegrationTests.Automation;

/// <summary>
/// S3 · Axe.Windows 2.4 (blueprint §10.2, support for the UIA rules) scans the lab surface, with voice numbers on
/// and off: no errors.
/// </summary>
[Collection(DesktopCollectionDefinition.Name)]
[Trait("Requires", "Desktop")]
[Trait("Req", "REG-06")]
public sealed class AxeTests(UiaSurfaceFixture surface) : IClassFixture<UiaSurfaceFixture>
{
    [DesktopFact]
    [Trait("Req", "ACC-001")]
    [Trait("Req", "REG-01")]
    public async Task The_lab_panel_has_no_Axe_errors()
    {
        WpfThread.Invoke(surface.Lab.Reset);
        var cursor = await surface.PrepareAsync();
        var scanner = ScannerFactory.CreateScanner(
            Config
                .Builder.ForProcessId(Environment.ProcessId)
                .WithOutputFileFormat(OutputFileFormat.None)
                .WithDPIAwareness(new UnchangedDpiAwareness())
                .Build()
        );

        var errors = Scan(scanner, "voice numbers off");
        WpfThread.Invoke(() => surface.Lab.SetVoiceNumbers(true));
        errors.Append(Scan(scanner, "voice numbers on"));

        errors.ToString().ShouldBeEmpty();
        (await surface.ForegroundViolationsAsync(cursor, "Axe scan")).ShouldBeEmpty();
    }

    private StringBuilder Scan(IScanner scanner, string state)
    {
        var output = scanner.Scan(
            new ScanOptions(scanId: null, scanRootWindowHandle: surface.SurfaceHandle)
        );
        var errors = new StringBuilder();
        foreach (var error in output.WindowScanOutputs.SelectMany(window => window.Errors))
        {
            error.Element.Properties.TryGetValue("Name", out var name);
            error.Element.Properties.TryGetValue("ControlType", out var controlType);
            errors
                .Append(state)
                .Append(": ")
                .Append(error.Rule.ID)
                .Append(" on ")
                .Append(controlType)
                .Append(" «")
                .Append(name)
                .Append("»: ")
                .AppendLine(error.Rule.Description);
        }

        return errors;
    }

    /// <summary>The test process hosts WPF windows: Axe must not change its DPI awareness while scanning.</summary>
    private sealed class UnchangedDpiAwareness : IDPIAwareness
    {
        public object? Enable() => null;

        public void Restore(object? dataFromEnable) { }
    }
}
