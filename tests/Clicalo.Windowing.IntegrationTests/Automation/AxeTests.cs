using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.Windowing.IntegrationTests.Automation.Lab;
using Clicalo.Windowing.IntegrationTests.Desktop;

namespace Clicalo.Windowing.IntegrationTests.Automation;

/// <summary>
/// S3 · Axe.Windows 2.4 (blueprint §10.2, support for the UIA rules) scans the lab surface, with voice numbers on
/// and off: no errors. Axe runs in another process, as an assistive technology does (<see cref="AxeScanRunner"/>).
/// </summary>
[Collection(DesktopCollectionDefinition.Name)]
[Trait("Requires", "Desktop")]
[Trait("Req", "REG-06")]
public sealed class AxeTests(UiaSurfaceFixture surface) : IClassFixture<UiaSurfaceFixture>
{
    private static readonly TimeSpan ScanTimeout = TimeSpan.FromMinutes(2);

    [DesktopFact]
    [Trait("Req", "ACC-001")]
    [Trait("Req", "REG-01")]
    public async Task The_lab_panel_has_no_Axe_errors()
    {
        WpfThread.Invoke(surface.Lab.Reset);
        var cursor = await surface.PrepareAsync();

        var errors = new StringBuilder();
        errors.Append(await ScanAsync("voice numbers off"));
        WpfThread.Invoke(() => surface.Lab.SetVoiceNumbers(true));
        errors.Append(await ScanAsync("voice numbers on"));

        errors.ToString().ShouldBeEmpty();
        (await surface.ForegroundViolationsAsync(cursor, "Axe scan")).ShouldBeEmpty();
    }

    private static ProcessStartInfo RunnerStart(string output, int processId, nint window)
    {
        var host =
            Environment.ProcessPath ?? throw new InvalidOperationException("No process path.");
        var start = new ProcessStartInfo(host)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(output)!,
        };
        if (
            string.Equals(
                Path.GetFileNameWithoutExtension(host),
                "dotnet",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            start.ArgumentList.Add(typeof(AxeScanRunner).Assembly.Location);
        }

        // The test executable's own xUnit v3 runner: only the scan, without logo or colors.
        start.ArgumentList.Add("-method");
        start.ArgumentList.Add(
            typeof(AxeScanRunner).FullName + "." + nameof(AxeScanRunner.Scan_the_target_window)
        );
        start.ArgumentList.Add("-noLogo");
        start.ArgumentList.Add("-noColor");
        start.Environment[AxeScanRunner.ProcessVariable] = processId.ToString(
            CultureInfo.InvariantCulture
        );
        start.Environment[AxeScanRunner.WindowVariable] = window.ToString(
            CultureInfo.InvariantCulture
        );
        start.Environment[AxeScanRunner.OutputVariable] = output;
        return start;
    }

    private async Task<string> ScanAsync(string state)
    {
        var folder = Directory.CreateTempSubdirectory("clicalo-axe-");
        try
        {
            var output = Path.Combine(folder.FullName, "axe.txt");
            using var runner =
                Process.Start(RunnerStart(output, Environment.ProcessId, surface.SurfaceHandle))
                ?? throw new InvalidOperationException("The Axe runner did not start.");
            var standardOutput = runner.StandardOutput.ReadToEndAsync(
                TestContext.Current.CancellationToken
            );
            var standardError = runner.StandardError.ReadToEndAsync(
                TestContext.Current.CancellationToken
            );
            using (
                var timeout = CancellationTokenSource.CreateLinkedTokenSource(
                    TestContext.Current.CancellationToken
                )
            )
            {
                timeout.CancelAfter(ScanTimeout);
                try
                {
                    await runner.WaitForExitAsync(timeout.Token);
                }
                catch (OperationCanceledException)
                {
                    runner.Kill(entireProcessTree: true);
                    throw;
                }
            }

            var lines = File.Exists(output) ? await File.ReadAllLinesAsync(output) : [];
            if (
                lines.Length == 0
                || !string.Equals(lines[0], AxeScanRunner.Completed, StringComparison.Ordinal)
            )
            {
                throw new InvalidOperationException(
                    "The Axe runner did not finish its scan ("
                        + state
                        + "). Its output:"
                        + Environment.NewLine
                        + await standardOutput
                        + await standardError
                );
            }

            var errors = new StringBuilder();
            foreach (var line in lines.Skip(1))
            {
                errors.Append(state).Append(": ").AppendLine(line);
            }

            return errors.ToString();
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }
}
