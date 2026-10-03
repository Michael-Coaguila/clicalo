using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using Clicalo.Application.Ports;
using Clicalo.Domain.Geometry;
using Clicalo.TestKit;
using Clicalo.TestKit.Windows.Input;
using Clicalo.TestKit.Windows.Probe;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.Windowing.IntegrationTests.Automation;
using Clicalo.Windowing.IntegrationTests.Desktop;
using Clicalo.Windowing.IntegrationTests.Windowing.Support;

namespace Clicalo.Windowing.IntegrationTests.Windowing;

/// <summary>
/// TEMPORARY diagnostic (m2/fix-desk): how long after a surface is shown does Windows route a touch to it instead of
/// the window below? The window below is InputProbe (in front, allowed), so a touch that falls through lands on it and
/// nowhere else; every trial checks that first. Only on the CI runner. Records outcomes; asserts nothing.
/// </summary>
[Collection(DesktopCollectionDefinition.Name)]
[Trait("Requires", "Desktop")]
public sealed class FirstContactExperimentTests
{
    private const uint DwmCloaked = 14;

    private static readonly TimeSpan Wait = TimeSpan.FromMilliseconds(1500);
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };
    private static readonly int[] Delays = [0, 15, 30, 45, 60, 90, 120, 200];

    [DesktopFact]
    public async Task How_soon_a_shown_surface_receives_touch()
    {
        Assert.SkipUnless(SystemContrastSwitch.IsAllowed, "Diagnostic: CI runner only.");
        var cancellationToken = TestContext.Current.CancellationToken;
        var results = new List<Dictionary<string, string>>();
        await using var probe = await InputProbeSession.StartAsync(cancellationToken);
        await probe.EnsureForegroundAsync(TimeSpan.FromSeconds(5), cancellationToken);
        using var lab = SurfaceLab.Create();
        var bounds = NativeSurface.Bounds(probe.Window);
        var spot = new PhysicalRect(bounds.CenterX - 32, bounds.CenterY - 32, 64, 64);
        using var finger = new SyntheticPointer(
            SyntheticPointerKind.Finger,
            [Environment.ProcessId, probe.ProcessId]
        );

        // Warm the device on the probe itself (in front, allowed).
        finger.Tap(bounds.CenterX, bounds.Top + 60);
        await Task.Delay(500, cancellationToken);

        var instance = 300;
        for (var repeat = 0; repeat < 5; repeat++)
        {
            foreach (var delay in Delays)
            {
                results.Add(
                    await TrialAsync(
                        "F-delay-" + delay.ToString("000", CultureInfo.InvariantCulture),
                        lab,
                        probe,
                        finger,
                        spot,
                        instance++,
                        async (_, shownAt) =>
                        {
                            var remaining =
                                TimeSpan.FromMilliseconds(delay)
                                - Stopwatch.GetElapsedTime(shownAt);
                            if (remaining > TimeSpan.Zero)
                            {
                                await Task.Delay(remaining, cancellationToken);
                            }
                        }
                    )
                );
            }
        }

        for (var repeat = 0; repeat < 20; repeat++)
        {
            results.Add(
                await TrialAsync(
                    "G-rendered-then-dwmflush",
                    lab,
                    probe,
                    finger,
                    spot,
                    instance++,
                    async (rendered, _) =>
                    {
                        await rendered.WaitAsync(Wait, cancellationToken);
                        _ = DwmFlush();
                    }
                )
            );
        }

        for (var repeat = 0; repeat < 20; repeat++)
        {
            results.Add(
                await TrialAsync(
                    "H-rendered-only",
                    lab,
                    probe,
                    finger,
                    spot,
                    instance++,
                    async (rendered, _) => await rendered.WaitAsync(Wait, cancellationToken)
                )
            );
        }

        var folder = RepoPaths.Combine("artifacts", "cl", "test-results");
        Directory.CreateDirectory(folder);
        File.WriteAllText(
            Path.Combine(folder, "first-contact-experiment.json"),
            JsonSerializer.Serialize(new { results }, Indented)
        );
        foreach (var group in results.GroupBy(r => r["phase"], StringComparer.Ordinal))
        {
            TestContext.Current.TestOutputHelper?.WriteLine(
                group.Key
                    + ": "
                    + string.Join(
                        ", ",
                        group
                            .GroupBy(r => r["outcome"], StringComparer.Ordinal)
                            .Select(o => o.Key + "=" + o.Count())
                    )
            );
        }
    }

    private static async Task<Dictionary<string, string>> TrialAsync(
        string phase,
        SurfaceLab lab,
        InputProbeSession probe,
        SyntheticPointer finger,
        PhysicalRect spot,
        int instance,
        Func<Task<long>, long, Task> beforeTap
    )
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fresh = lab.CreateSurface(SurfaceKind.Bubble, instance, 64, 64);
        var rendered = new TaskCompletionSource<long>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var shownAt = WpfThread.Invoke(() =>
        {
            fresh.ContentRendered += (_, _) => rendered.TrySetResult(Stopwatch.GetTimestamp());
            fresh.MovePassive(spot);
            fresh.ShowPassive();
            return Stopwatch.GetTimestamp();
        });
        var x = spot.Left + 32;
        var y = spot.Top + 32;
        var result = new Dictionary<string, string>(StringComparer.Ordinal) { ["phase"] = phase };
        try
        {
            await beforeTap(rendered.Task, shownAt);
            var below = WindowsAt.Describe(x, y);
            result["below"] = string.Join(" > ", below.Take(2));
            if (below.Count < 2 || !below[1].Contains("InputProbe", StringComparison.Ordinal))
            {
                result["outcome"] = "skipped: the probe is not right below";
                return result;
            }

            var cursor = probe.Cursor;
            var ups = fresh.PointerUps;
            result["cloakedAtTap"] = IsCloaked(fresh.Handle).ToString(CultureInfo.InvariantCulture);
            result["renderedAtTap"] = rendered.Task.IsCompleted.ToString(
                CultureInfo.InvariantCulture
            );
            result["tapAfterShowMs"] = Ms(Stopwatch.GetElapsedTime(shownAt));
            finger.Tap(x, y);
            var started = Stopwatch.GetTimestamp();
            string outcome;
            while (true)
            {
                if (fresh.PointerUps > ups)
                {
                    outcome = "surface";
                    break;
                }

                if (probe.EventsSince(cursor).OfType<MouseButtonEvent>().Any())
                {
                    outcome = "probe (fell through)";
                    break;
                }

                if (Stopwatch.GetElapsedTime(started) > Wait)
                {
                    outcome = "none";
                    break;
                }

                await Task.Delay(5, cancellationToken);
            }

            result["outcome"] = outcome;
            result["renderedAfterShowMs"] = rendered.Task.IsCompleted
                ? Ms(Stopwatch.GetElapsedTime(shownAt, await rendered.Task))
                : "not yet";
        }
        catch (InjectionRefusedException ex)
        {
            result["outcome"] = "refused: " + ex.Message;
        }
        finally
        {
            lab.Close(fresh);
            await Task.Delay(150, cancellationToken);
        }

        return result;
    }

    private static string Ms(TimeSpan elapsed) =>
        elapsed.TotalMilliseconds.ToString("0.0", CultureInfo.InvariantCulture);

    private static bool IsCloaked(nint window) =>
        DwmGetWindowAttribute(window, DwmCloaked, out var cloaked, sizeof(int)) == 0
        && cloaked != 0;

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int DwmFlush();

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int DwmGetWindowAttribute(
        nint window,
        uint attribute,
        out int value,
        int size
    );
}
