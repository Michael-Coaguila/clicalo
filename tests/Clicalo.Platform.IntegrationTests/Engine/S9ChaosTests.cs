using System.Diagnostics;
using System.Globalization;
using Clicalo.Platform.IntegrationTests.Desktop;
using Clicalo.TestKit;
using Clicalo.TestKit.Windows.Input;
using Clicalo.TestKit.Windows.Probe;

namespace Clicalo.Platform.IntegrationTests.Engine;

/// <summary>
/// Spike S9 as tests (blueprint §15.1, §7.10 item 5, ADR-0022): <c>TerminateProcess</c> of a main process that holds
/// Ctrl+Shift, a drag or a macro step, with the real Sentinel and the real injector; nothing may be down 1 s after the
/// death, 50 times out of 50. Nightly only (<c>Category=Chaos</c>, <see cref="ChaosEnvironment"/>): it kills a process
/// with keys held. Required before every release.
/// </summary>
[Collection(DesktopCollectionDefinition.Name)]
[Trait("Requires", "Desktop")]
[Trait("Category", "Chaos")]
[Trait("Req", "SEG-006")]
[Trait("Req", "SEG-007")]
[Trait("Req", "REG-03")]
public sealed class S9ChaosTests(DesktopProbeFixture desktop)
{
    /// <summary>The S9 criterion: 50 of 50.</summary>
    public const int Attempts = 50;

    /// <summary>The criterion of ADR-0022: nothing down 1 s after the death.</summary>
    public static readonly TimeSpan Budget = TimeSpan.FromSeconds(1);

    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(15);

    [ChaosFact]
    public async Task Killing_a_main_process_that_holds_ctrl_shift_leaves_nothing_down_within_1_s_50_of_50()
    {
        var late = new List<long>();
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            var elapsed = await KillAndMeasureAsync(
                "hold",
                static events => Ups(events, VirtualKeyCode.LeftControl, VirtualKeyCode.LeftShift)
            );
            if (elapsed > Budget.TotalMilliseconds)
            {
                late.Add(elapsed);
            }
        }

        late.ShouldBeEmpty("releases later than " + Budget.TotalMilliseconds + " ms");
    }

    [ChaosFact]
    public async Task Killing_a_main_process_in_the_middle_of_a_macro_step_releases_its_keys()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var elapsed = await KillAndMeasureAsync(
                "macro",
                static events => Ups(events, VirtualKeyCode.LeftControl, VirtualKeyCode.C)
            );
            elapsed.ShouldBeLessThanOrEqualTo((long)Budget.TotalMilliseconds);
        }
    }

    [ChaosFact]
    [Trait("Req", "EJE-007")]
    public async Task Killing_a_main_process_in_the_middle_of_a_drag_releases_the_button()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var elapsed = await KillAndMeasureAsync(
                "drag",
                static events =>
                    ProductProbeEvents
                        .Buttons(events)
                        .FirstOrDefault(static b => b.Button == MouseButton.Left && !b.IsDown)
                        is { } up
                        ? up.MessageTime
                        : null
            );
            elapsed.ShouldBeLessThanOrEqualTo((long)Budget.TotalMilliseconds);
        }
    }

    private static int? Ups(IReadOnlyList<ProbeEvent> events, params VirtualKeyCode[] keys)
    {
        var ups = ProductProbeEvents.Keys(events).Where(static key => key.IsRelease).ToList();
        return keys.All(key => ups.Any(up => up.SideVirtualKey == key))
            ? ups.Where(up => keys.Contains(up.SideVirtualKey)).Max(static up => up.MessageTime)
            : null;
    }

    private async Task<long> KillAndMeasureAsync(
        string scenario,
        Func<IReadOnlyList<ProbeEvent>, int?> releasedAt
    )
    {
        var cursor = await desktop.PrepareAsync();
        var start = new ProcessStartInfo(TestExecutable())
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        };
        foreach (
            var argument in new[]
            {
                ChaosMain.Flag,
                SentinelPath(),
                scenario,
                ((long)desktop.Probe.Window).ToString(CultureInfo.InvariantCulture),
            }
        )
        {
            start.ArgumentList.Add(argument);
        }

        using var main = Process.Start(start)!;
        using var ready = new CancellationTokenSource(ReadyTimeout);
        var line = await main.StandardOutput.ReadLineAsync(ready.Token);
        line.ShouldNotBeNull().ShouldStartWith(ChaosMain.Ready);

        var killedAt = Environment.TickCount64;
        main.Kill();
        var events = await desktop.CollectAsync(cursor, e => releasedAt(e) is not null);
        await main.WaitForExitAsync(TestContext.Current.CancellationToken);

        // GetMessageTime and TickCount64 count the same milliseconds since boot (the former wraps at 32 bits).
        return unchecked(releasedAt(events)!.Value - (int)killedAt);
    }

    private static string TestExecutable() =>
        Path.ChangeExtension(typeof(ChaosMain).Assembly.Location, ".exe");

    private static string SentinelPath()
    {
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Name;
        return RepoPaths.Combine(
            "artifacts",
            "bin",
            "Clicalo.Sentinel",
            configuration,
            "Clicalo.Sentinel.exe"
        );
    }
}
