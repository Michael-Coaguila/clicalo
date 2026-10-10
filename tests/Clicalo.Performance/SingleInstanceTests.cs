using System.Diagnostics;
using System.IO.Pipes;

namespace Clicalo.Performance;

/// <summary>
/// The single instance of the published app, as a process (SIS-003, NFR-018, ADR-0010): a second start in the same
/// session shows the running instance and ends without a second engine, and a second start that finds the pipe held by
/// a process that is not Clícalo sends nothing and says so. They live here because this is the only project that runs
/// the published <c>Clicalo.exe</c> as a process; they run with <c>cl perf</c>, always with <c>--no-input</c>.
/// </summary>
[Trait("Requires", "Desktop")]
[Trait("Category", "Perf")]
[Trait("Req", "SIS-003")]
[Trait("Req", "NFR-018")]
public sealed class SingleInstanceTests
{
    private static readonly TimeSpan SecondStartTimeout = TimeSpan.FromSeconds(10);

    [PerfFact]
    public void A_second_start_shows_the_running_instance_and_ends()
    {
        var variant = PerfEnvironment.Variants[0];
        using var first = AppLaunch.Start(variant, sendInput: false);

        var code = RunSecondStart(variant);

        code.ShouldBe(0, "the running instance answered «show»");
        first.Process.HasExited.ShouldBeFalse("the first instance keeps running");
        PanelWindows.Find(first.Process.Id).ShouldNotBeNull("its panel is on screen");
    }

    [PerfFact]
    [Trait("Req", "LOG-007")]
    public async Task A_second_start_sends_nothing_to_a_pipe_that_is_not_Clicalo()
    {
        var variant = PerfEnvironment.Variants[0];

        // The name taken before Clícalo starts: its server then finds the name held (FILE_FLAG_FIRST_PIPE_INSTANCE)
        // and runs without its pipe, and a second start must detect that the server is not Clícalo.
        using var squatter = new NamedPipeServerStream(
            RunningInstance.PipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Message,
            PipeOptions.Asynchronous
        );
        var connected = squatter.WaitForConnectionAsync(TestContext.Current.CancellationToken);
        using var first = AppLaunch.Start(variant, sendInput: false);

        var code = RunSecondStart(variant);

        code.ShouldBe(
            3,
            "ipc.squat_detected: the second start refused to talk to a pipe that is not Clícalo"
        );
        if (connected.IsCompletedSuccessfully)
        {
            // The client connected, checked who serves the pipe and left: the squatter reads the end, never a request.
            var read = await squatter.ReadAsync(
                new byte[64],
                TestContext.Current.CancellationToken
            );
            read.ShouldBe(0, "nothing was sent to the squatter");
        }
    }

    private static int RunSecondStart(AppVariant variant)
    {
        var data = Path.Combine(Path.GetTempPath(), "clicalo-perf-data", "second-start");
        var start = new ProcessStartInfo(variant.Executable) { UseShellExecute = false };
        start.ArgumentList.Add("--no-input");
        start.ArgumentList.Add("--data");
        start.ArgumentList.Add(data);
        using var second =
            Process.Start(start) ?? throw new InvalidOperationException("The second start failed.");
        if (!second.WaitForExit(SecondStartTimeout))
        {
            second.Kill(entireProcessTree: true);
            throw new TimeoutException("The second start did not end: two instances would run.");
        }

        return second.ExitCode;
    }
}
