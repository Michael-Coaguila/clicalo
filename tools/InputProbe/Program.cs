using Clicalo.Tools.InputProbe.Protocol;
using Windows.Win32;
using Windows.Win32.UI.HiDpi;

namespace Clicalo.Tools.InputProbe;

/// <summary>
/// InputProbe: a raw Win32 window (no WPF, no WinForms) that records every input message it receives and streams
/// it, one JSON object per line, to the named pipe given by <c>--pipe &lt;name&gt;</c> (blueprint §10.1).
/// It is the physical truth of the integration tests; see <c>tools/InputProbe/README.md</c> for the protocol.
/// </summary>
internal static class Program
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan DrainTimeout = TimeSpan.FromSeconds(2);

    [STAThread]
    private static int Main(string[] args)
    {
        var arguments = ProbeArguments.TryParse(args);
        if (arguments is null)
        {
            return ProbeProtocol.ExitUsage;
        }

        // Physical pixels for mouse coordinates, whatever the monitor scale. Failure only means it was already set.
        _ = PInvoke.SetProcessDpiAwarenessContext(
            DPI_AWARENESS_CONTEXT.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2
        );

        using var pipe = ProbePipe.TryConnect(arguments.PipeName, ConnectTimeout);
        if (pipe is null)
        {
            return ProbeProtocol.ExitPipe;
        }

        using var events = new EventEmitter(pipe);
        var window = new ProbeWindow(events);
        if (!window.TryCreate(out var failure))
        {
            events.Error(failure);
            pipe.Start(static _ => { }, static () => { });
            pipe.Complete(DrainTimeout);
            return ProbeProtocol.ExitWindow;
        }

        pipe.Start(window.OnCommandLine, window.OnPipeClosed);
        window.ShowAndAnnounceReady();
        var exitCode = ProbeWindow.RunMessageLoop();
        pipe.Complete(DrainTimeout);
        return exitCode;
    }
}
