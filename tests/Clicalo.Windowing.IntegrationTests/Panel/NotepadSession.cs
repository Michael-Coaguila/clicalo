using System.Diagnostics;

namespace Clicalo.Windowing.IntegrationTests.MinimalPanel;

/// <summary>
/// A Notepad of its own for the tray test of M2 («Bloc de notas activo → menú → Soltar todo → el foco vuelve al Bloc de
/// notas», blueprint §10.1 and §14): it starts <c>notepad.exe</c> and waits for the main window of a Notepad process that
/// did not exist before, so an instance the person already had open is never used, touched or closed. Only continuous
/// integration opens it. Nothing is ever typed into it; disposing it closes only the processes it started.
/// </summary>
internal sealed class NotepadSession : IDisposable
{
    private const string ProcessName = "notepad";
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    private readonly HashSet<int> _started;

    private NotepadSession(nint window, int processId, HashSet<int> started)
    {
        Window = window;
        ProcessId = processId;
        _started = started;
    }

    /// <summary>The Notepad main window.</summary>
    public nint Window { get; }

    /// <summary>The process that owns <see cref="Window"/>.</summary>
    public int ProcessId { get; }

    /// <summary>Starts Notepad and waits for the main window of a Notepad process that is new.</summary>
    /// <param name="cancellationToken">Stops waiting.</param>
    public static async Task<NotepadSession> StartAsync(CancellationToken cancellationToken)
    {
        var before = Ids();
        // On Windows 11 notepad.exe may hand over to the packaged Notepad and exit: the window is looked up by name.
        Process.Start(new ProcessStartInfo("notepad.exe") { UseShellExecute = false })?.Dispose();

        var started = new HashSet<int>();
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < StartTimeout)
        {
            foreach (var process in Process.GetProcessesByName(ProcessName))
            {
                using (process)
                {
                    if (before.Contains(process.Id))
                    {
                        continue;
                    }

                    started.Add(process.Id);
                    process.Refresh();
                    if (process.MainWindowHandle != 0)
                    {
                        return new NotepadSession(process.MainWindowHandle, process.Id, started);
                    }
                }
            }

            await Task.Delay(PollInterval, cancellationToken);
        }

        Close(started);
        throw new TimeoutException(
            "notepad.exe did not show a window of a new Notepad process within "
                + StartTimeout.TotalSeconds.ToString(
                    System.Globalization.CultureInfo.InvariantCulture
                )
                + " s."
        );
    }

    /// <summary>Closes the Notepad processes this session started, never another one.</summary>
    public void Dispose() => Close(_started);

    private static HashSet<int> Ids()
    {
        var ids = new HashSet<int>();
        foreach (var process in Process.GetProcessesByName(ProcessName))
        {
            using (process)
            {
                ids.Add(process.Id);
            }
        }

        return ids;
    }

    private static void Close(HashSet<int> started)
    {
        foreach (var id in started)
        {
            try
            {
                using var process = Process.GetProcessById(id);
                if (
                    string.Equals(
                        process.ProcessName,
                        ProcessName,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    // Nothing was typed, so there is nothing to save: ending it loses no data.
                    process.Kill();
                    _ = process.WaitForExit(TimeSpan.FromSeconds(5));
                }
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                // Already gone.
            }
        }
    }
}
