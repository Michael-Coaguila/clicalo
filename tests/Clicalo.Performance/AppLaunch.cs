using System.Diagnostics;
using System.Globalization;

namespace Clicalo.Performance;

/// <summary>
/// One start of a published <c>Clicalo.exe</c> with its own empty data folder (<c>--data</c>), measured from the
/// creation of the process to the first frame of the panel: the app sets the event named by
/// <c>CLICALO_FIRST_FRAME_EVENT</c> when the panel's content is rendered. Without key sending unless the measurement
/// runs in continuous integration (<c>--no-input</c>). Disposing it ends the process and its children (Sentinel).
/// </summary>
internal sealed class AppLaunch : IDisposable
{
    /// <summary>The environment variable the app reads (App/Lifecycle/FirstFrameSignal).</summary>
    public const string FirstFrameVariable = "CLICALO_FIRST_FRAME_EVENT";

    /// <summary>Longest wait for the first frame.</summary>
    public static readonly TimeSpan FirstFrameTimeout = TimeSpan.FromSeconds(30);

    private readonly string _data;
    private bool _disposed;

    private AppLaunch(Process process, string data, TimeSpan firstFrame)
    {
        Process = process;
        _data = data;
        FirstFrame = firstFrame;
    }

    /// <summary>The running Clicalo.exe.</summary>
    public Process Process { get; }

    /// <summary>From the creation of the process to the first frame.</summary>
    public TimeSpan FirstFrame { get; }

    /// <summary>Starts <paramref name="variant"/> and waits for its first frame.</summary>
    /// <param name="variant">The publication.</param>
    /// <param name="sendInput">False adds <c>--no-input</c>: nothing can be injected.</param>
    /// <param name="extraArguments">More options (<c>--guardian after-first-frame</c>).</param>
    public static AppLaunch Start(
        AppVariant variant,
        bool sendInput,
        params string[] extraArguments
    )
    {
        ArgumentNullException.ThrowIfNull(variant);
        var data = Path.Combine(
            Path.GetTempPath(),
            "clicalo-perf-data",
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)
        );
        var name =
            @"Local\Clicalo.Perf." + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        using var firstFrame = new EventWaitHandle(false, EventResetMode.ManualReset, name);
        var start = new ProcessStartInfo(variant.Executable) { UseShellExecute = false };
        start.ArgumentList.Add("--data");
        start.ArgumentList.Add(data);
        if (!sendInput)
        {
            start.ArgumentList.Add("--no-input");
        }

        foreach (var argument in extraArguments)
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment[FirstFrameVariable] = name;
        var process =
            Process.Start(start)
            ?? throw new InvalidOperationException(
                "Clicalo.exe did not start: " + variant.Executable
            );
        var deadline = Stopwatch.GetTimestamp();
        while (Stopwatch.GetElapsedTime(deadline) < FirstFrameTimeout)
        {
            if (firstFrame.WaitOne(TimeSpan.FromMilliseconds(25)))
            {
                var at = DateTime.Now;
                return new AppLaunch(process, data, at - process.StartTime);
            }

            if (process.HasExited)
            {
                var code = process.ExitCode;
                process.Dispose();
                throw new InvalidOperationException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Clicalo.exe ({variant.Name}) ended with code {code} before its first frame: 70 is a failed start (its log says why), 0 means another Clícalo of this session was already running."
                    )
                );
            }
        }

        End(process);
        throw new TimeoutException(
            "Clicalo.exe (" + variant.Name + ") showed no frame within " + FirstFrameTimeout + "."
        );
    }

    /// <summary>The working set and private bytes after <paramref name="settle"/>.</summary>
    /// <param name="settle">How long to let the start settle.</param>
    /// <param name="cancellationToken">Stops waiting.</param>
    public async Task<(long WorkingSet, long Private)> SettledMemoryAsync(
        TimeSpan settle,
        CancellationToken cancellationToken
    )
    {
        await Task.Delay(settle, cancellationToken);
        Process.Refresh();
        return (Process.WorkingSet64, Process.PrivateMemorySize64);
    }

    /// <summary>Ends Clicalo.exe and Sentinel and deletes the data folder.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        End(Process);
        try
        {
            if (Directory.Exists(_data))
            {
                Directory.Delete(_data, recursive: true);
            }
        }
        catch (IOException)
        {
            // A file still held for a moment: the folder is under %TEMP% and only holds test data.
        }
        catch (UnauthorizedAccessException)
        {
            // Same.
        }
    }

    private static void End(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                // The whole tree: Sentinel is a child and must not relaunch the app it guards.
                process.Kill(entireProcessTree: true);
                process.WaitForExit(TimeSpan.FromSeconds(10));
            }
        }
        finally
        {
            process.Dispose();
        }
    }
}
