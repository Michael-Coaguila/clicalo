using System.Globalization;
using System.IO;
using System.Text;
using Axe.Windows.Automation;
using Axe.Windows.Automation.Data;

namespace Clicalo.Windowing.IntegrationTests.Automation;

/// <summary>
/// The out-of-process half of <see cref="AxeTests"/>. Axe.Windows cannot scan a window of its own process (its UI
/// Automation client gets <c>0x80040201</c> from <c>ElementFromHandle</c>, found in spike S3), and a real assistive
/// technology is always another process anyway. So <see cref="AxeTests"/> starts this test executable again with
/// only this test selected and the target in environment variables; this test scans the target window and writes
/// the errors, one per line, to the output file. Without those variables it is skipped.
/// </summary>
[Trait("Requires", "Desktop")]
public sealed class AxeScanRunner
{
    /// <summary>Process id of the window to scan.</summary>
    public const string ProcessVariable = "CLICALO_AXE_TARGET_PID";

    /// <summary>Handle of the window to scan, in decimal.</summary>
    public const string WindowVariable = "CLICALO_AXE_TARGET_WINDOW";

    /// <summary>File that receives the result: <see cref="Completed"/>, then one error per line.</summary>
    public const string OutputVariable = "CLICALO_AXE_OUTPUT";

    /// <summary>First line of a finished scan, so a crash is not read as «no errors».</summary>
    public const string Completed = "axe-scan-completed";

    /// <summary>True when a parent <see cref="AxeTests"/> asked for a scan.</summary>
    public static bool IsRequested =>
        Environment.GetEnvironmentVariable(OutputVariable) is { Length: > 0 };

    [Fact(
        Skip = "Only runs when AxeTests starts it with a target window.",
        SkipUnless = nameof(IsRequested),
        SkipType = typeof(AxeScanRunner)
    )]
    public void Scan_the_target_window()
    {
        var processId = int.Parse(
            Environment.GetEnvironmentVariable(ProcessVariable)!,
            CultureInfo.InvariantCulture
        );
        var window = nint.Parse(
            Environment.GetEnvironmentVariable(WindowVariable)!,
            CultureInfo.InvariantCulture
        );
        var scanner = ScannerFactory.CreateScanner(
            Config
                .Builder.ForProcessId(processId)
                .WithOutputFileFormat(OutputFileFormat.None)
                .Build()
        );

        var output = scanner.Scan(new ScanOptions(scanId: null, scanRootWindowHandle: window));

        var result = new StringBuilder().AppendLine(Completed);
        foreach (var error in output.WindowScanOutputs.SelectMany(scan => scan.Errors))
        {
            error.Element.Properties.TryGetValue("ControlType", out var controlType);
            error.Element.Properties.TryGetValue("Name", out var name);
            result.AppendLine(
                CultureInfo.InvariantCulture,
                $"{error.Rule.ID} on {controlType} «{name}»: {error.Rule.Description}"
            );
        }

        File.WriteAllText(
            Environment.GetEnvironmentVariable(OutputVariable)!,
            result.ToString(),
            Encoding.UTF8
        );
    }
}
