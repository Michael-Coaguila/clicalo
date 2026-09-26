namespace Clicalo.App.SingleInstance;

/// <summary>
/// The named mutex <c>Local\Clicalo.{sidHash}.Instance</c> (blueprint §3.4, SIS-003, NFR-018): the process that creates
/// it is the only instance of the session; a second start finds it and asks the first to show itself instead of
/// running a second engine on the same keyboard. The handle is kept open until the process ends (the object dies with
/// its last handle), so ownership and abandonment never matter.
/// </summary>
internal sealed class InstanceMutex : IDisposable
{
    private readonly Mutex _mutex;

    private InstanceMutex(Mutex mutex, bool isFirst)
    {
        _mutex = mutex;
        IsFirst = isFirst;
    }

    /// <summary>Whether this process created the mutex: it is the running instance.</summary>
    public bool IsFirst { get; }

    /// <summary>Opens or creates the mutex of <paramref name="identity"/>.</summary>
    /// <param name="identity">The names of the instance.</param>
    public static InstanceMutex Open(InstanceIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        var mutex = new Mutex(initiallyOwned: false, identity.MutexName, out var createdNew);
        return new InstanceMutex(mutex, createdNew);
    }

    /// <summary>Closes the handle: once every handle is closed, the next start is the first again.</summary>
    public void Dispose() => _mutex.Dispose();
}
