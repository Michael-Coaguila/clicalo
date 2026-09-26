using System.Diagnostics;
using System.Globalization;

namespace Clicalo.TestKit.Windows;

/// <summary>
/// One user of the interactive desktop at a time: every desktop test process and every SpikeLab session holds the
/// named mutex <see cref="MutexName"/> while it runs. Two of them at once take the foreground from each other's
/// InputProbe (or from the app under a spike script), and the failure they produce looks like a product defect.
/// </summary>
/// <remarks>
/// <para>
/// A mutex belongs to the thread that acquired it, and the async code that holds this lock may resume on any thread,
/// so a dedicated background thread acquires it, waits for <see cref="Dispose"/> and releases it. A process that
/// ends without releasing it abandons the mutex, and the next one acquires it at once.
/// </para>
/// <para>
/// A desktop test that starts the test executable again as a helper (the out-of-process UI Automation clients of
/// <c>AxeTests</c> and <c>OutOfProcessUiaTests</c>) runs inside the session of its parent: the holder writes its process
/// id to <see cref="HolderVariable"/>, the child inherits it, and <see cref="AcquireAsync"/> in the child returns at
/// once without taking the mutex, instead of waiting for its own parent until it fails.
/// </para>
/// </remarks>
public sealed class DesktopSessionLock : IDisposable
{
    /// <summary>The name of the mutex, the same in every desktop test process and in SpikeLab.</summary>
    public const string MutexName = @"Global\Clicalo.DesktopTests";

    /// <summary>
    /// The environment variable in which the test process that holds the lock names itself, for the helper processes
    /// it starts.
    /// </summary>
    public const string HolderVariable = "CLICALO_DESKTOP_SESSION_HOLDER";

    /// <summary>What a desktop test process says when it could not take the lock.</summary>
    public const string BusyMessage =
        "Another desktop test run or an open SpikeLab session holds the desktop ("
        + MutexName
        + "). Their InputProbe and windows would take the foreground from this run's: close SpikeLab or wait for the "
        + "other run to end, then run the desktop tests again.";

    /// <summary>How long a desktop test process waits for another one (or SpikeLab) before failing.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);

    private readonly ManualResetEventSlim _release = new();
    private readonly Thread? _owner;
    private int _disposed;

    private DesktopSessionLock(Thread? owner) => _owner = owner;

    /// <summary>True when this lock only stands for the one its parent process holds (see <see cref="HolderVariable"/>).</summary>
    public bool IsInherited => _owner is null;

    /// <summary>
    /// Takes the lock, waiting up to <paramref name="timeout"/> (zero: only if it is free); null when someone else still
    /// holds it, and the caller says so in words.
    /// </summary>
    public static DesktopSessionLock? TryAcquire(TimeSpan timeout)
    {
        using var decided = new ManualResetEventSlim();
        var owns = false;
        DesktopSessionLock? held = null;
        var owner = new Thread(() =>
        {
            using var mutex = new Mutex(initiallyOwned: false, MutexName);
            try
            {
                owns = mutex.WaitOne(timeout);
            }
            catch (AbandonedMutexException)
            {
                // The previous holder ended without releasing it: it is ours now.
                owns = true;
            }

            var release = held!._release;
            decided.Set();
            if (!owns)
            {
                return;
            }

            release.Wait();
            mutex.ReleaseMutex();
            release.Dispose();
        })
        {
            IsBackground = true,
            Name = "Clicalo desktop session lock",
        };
        held = new DesktopSessionLock(owner);
        owner.Start();
        decided.Wait();
        if (owns)
        {
            return held;
        }

        _ = owner.Join(TimeSpan.FromSeconds(5));
        held._release.Dispose();
        return null;
    }

    /// <summary>
    /// Takes the lock for a desktop test process, or fails with <paramref name="busyMessage"/> after
    /// <see cref="DefaultTimeout"/>. In a helper process started by the holder, returns an inherited lock at once.
    /// </summary>
    public static async Task<DesktopSessionLock> AcquireAsync(string busyMessage)
    {
        if (
            IsHeldByParent(
                Environment.GetEnvironmentVariable(HolderVariable),
                Environment.ProcessId,
                IsAlive
            )
        )
        {
            return new DesktopSessionLock(owner: null);
        }

        var held =
            await Task.Run(() => TryAcquire(DefaultTimeout)).ConfigureAwait(false)
            ?? throw new InvalidOperationException(busyMessage);

        // Inherited by the helper processes this process starts from now on.
        Environment.SetEnvironmentVariable(
            HolderVariable,
            Environment.ProcessId.ToString(CultureInfo.InvariantCulture)
        );
        return held;
    }

    /// <summary>
    /// True when <paramref name="holder"/> (the inherited <see cref="HolderVariable"/>) names another process that is
    /// still running: the process that started this one holds the lock for the whole session.
    /// </summary>
    internal static bool IsHeldByParent(
        string? holder,
        int currentProcessId,
        Func<int, bool> isAlive
    )
    {
        ArgumentNullException.ThrowIfNull(isAlive);
        return int.TryParse(
                holder,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var processId
            )
            && processId > 0
            && processId != currentProcessId
            && isAlive(processId);
    }

    /// <summary>Releases the lock (an inherited one releases nothing).</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0 || _owner is null)
        {
            return;
        }

        if (
            string.Equals(
                Environment.GetEnvironmentVariable(HolderVariable),
                Environment.ProcessId.ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal
            )
        )
        {
            Environment.SetEnvironmentVariable(HolderVariable, null);
        }

        _release.Set();
        _ = _owner.Join(TimeSpan.FromSeconds(5));
    }

    private static bool IsAlive(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // It exists, but this process may not query it.
            return true;
        }
    }
}
