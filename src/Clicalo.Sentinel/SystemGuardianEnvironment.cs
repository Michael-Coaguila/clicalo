using System.Collections.Immutable;
using System.ComponentModel;
using Clicalo.Platform.Core.Guardian;

namespace Clicalo.Sentinel;

/// <summary>
/// The machine as Sentinel sees it: the inherited parent handle and pipe, the crash journal under
/// <c>%LocalAppData%\Clicalo</c> (read only: the relaunched main process records the crash it was started after) and
/// <c>Clicalo.exe</c> next to Sentinel (they are published together and never mixed, ADR-0018).
/// </summary>
/// <param name="startInfo">The start-up contract.</param>
/// <param name="time">Schedules the pause between two attempts of a refused release.</param>
internal sealed class SystemGuardianEnvironment(SentinelStartInfo startInfo, TimeProvider time)
    : IGuardianEnvironment
{
    /// <summary>The main executable, next to Sentinel.</summary>
    public const string MainExecutable = "Clicalo.exe";

    /// <inheritdoc />
    public GuardianWake WaitForParentOrPipe() =>
        GuardianHandles.WaitForExitOrBrokenPipe(startInfo.ParentProcess, startInfo.HeartbeatPipe);

    /// <inheritdoc />
    public bool WaitForParentExit(TimeSpan timeout) =>
        GuardianHandles.WaitForExit(startInfo.ParentProcess, timeout);

    /// <inheritdoc />
    public void WaitBeforeRetry(TimeSpan interval)
    {
        // A one-shot timer of the TimeProvider sets a kernel event; the thread blocks on it in between.
        using var elapsed = new ManualResetEventSlim(initialState: false, spinCount: 0);
        using (
            time.CreateTimer(
                static state => ((ManualResetEventSlim)state!).Set(),
                elapsed,
                interval,
                Timeout.InfiniteTimeSpan
            )
        )
        {
            elapsed.Wait();
        }
    }

    /// <inheritdoc />
    public ImmutableArray<DateTimeOffset> RecentCrashes()
    {
        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Clicalo",
                CrashJournal.FileName
            );
            return File.Exists(path) ? CrashJournal.Parse(File.ReadAllBytes(path)) : [];
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <inheritdoc />
    public bool Relaunch(ImmutableArray<string> arguments)
    {
        try
        {
            using var main = GuardianProcess.Start(
                Path.Combine(AppContext.BaseDirectory, MainExecutable),
                arguments,
                []
            );
            return true;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }
}
