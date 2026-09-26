namespace Clicalo.TestKit.Windows;

/// <summary>
/// One user of the interactive desktop at a time: every desktop test process and every SpikeLab session holds the
/// named mutex <see cref="MutexName"/> while it runs. Two of them at once take the foreground from each other's
/// InputProbe (or from the app under a spike script), and the failure they produce looks like a product defect.
/// </summary>
/// <remarks>
/// A mutex belongs to the thread that acquired it, and the async code that holds this lock may resume on any thread,
/// so a dedicated background thread acquires it, waits for <see cref="Dispose"/> and releases it. A process that
/// ends without releasing it abandons the mutex, and the next one acquires it at once.
/// </remarks>
public sealed class DesktopSessionLock : IDisposable
{
    /// <summary>The name of the mutex, the same in every desktop test process and in SpikeLab.</summary>
    public const string MutexName = @"Global\Clicalo.DesktopTests";

    /// <summary>What a desktop test process says when it could not take the lock.</summary>
    public const string BusyMessage =
        "Another desktop test run or an open SpikeLab session holds the desktop ("
        + MutexName
        + "). Their InputProbe and windows would take the foreground from this run's: close SpikeLab or wait for the "
        + "other run to end, then run the desktop tests again.";

    /// <summary>How long a desktop test process waits for another one (or SpikeLab) before failing.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);

    private readonly ManualResetEventSlim _release = new();
    private readonly Thread _owner;
    private int _disposed;

    private DesktopSessionLock(Thread owner) => _owner = owner;

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

    /// <summary>Takes the lock or fails with <paramref name="busyMessage"/> after <see cref="DefaultTimeout"/>.</summary>
    public static async Task<DesktopSessionLock> AcquireAsync(string busyMessage) =>
        await Task.Run(() => TryAcquire(DefaultTimeout)).ConfigureAwait(false)
        ?? throw new InvalidOperationException(busyMessage);

    /// <summary>Releases the lock.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _release.Set();
        _ = _owner.Join(TimeSpan.FromSeconds(5));
    }
}
