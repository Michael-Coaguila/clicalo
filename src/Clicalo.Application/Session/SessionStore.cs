namespace Clicalo.Application.Session;

/// <summary>
/// The single writer of the <see cref="PanelSession"/> (blueprint §3.2 rule 2, §6.4, ADR-0003): it belongs to the
/// Surfaces role of the UI thread that created it, and only that thread may <see cref="Dispatch"/>. Any thread may read
/// <see cref="Current"/>, which is published with <c>Volatile.Write</c>. ArchUnit keeps its mutating members inside the
/// Surfaces role (<c>OnlyTheSurfacesRoleWritesSessionAndInteraction</c>).
/// </summary>
public sealed class SessionStore
{
    private readonly int _ownerThread;
    private PanelSession _current;

    /// <summary>Creates the store on the thread of the Surfaces role, which becomes its only writer.</summary>
    /// <param name="initial">The first session.</param>
    public SessionStore(PanelSession initial)
    {
        ArgumentNullException.ThrowIfNull(initial);
        _current = initial;
        _ownerThread = Environment.CurrentManagedThreadId;
    }

    /// <summary>Raised on the owner thread after every change.</summary>
    public event EventHandler<SessionChangedEventArgs>? Changed;

    /// <summary>The current session; safe to read from any thread.</summary>
    public PanelSession Current => Volatile.Read(ref _current);

    /// <summary>Whether the calling thread is the store's only writer.</summary>
    public bool CheckAccess() => Environment.CurrentManagedThreadId == _ownerThread;

    /// <summary>Applies <paramref name="action"/> through <see cref="PanelSessionReducer"/>.</summary>
    /// <param name="action">The intention.</param>
    /// <returns>Whether the session changed.</returns>
    /// <exception cref="InvalidOperationException">Called from a thread other than the Surfaces role (a defect).</exception>
    public bool Dispatch(SessionAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (!CheckAccess())
        {
            throw new InvalidOperationException(
                "SessionStore is written only by the Surfaces role, on the thread that created it (blueprint §3.2)."
            );
        }

        var previous = _current;
        var next = PanelSessionReducer.Reduce(previous, action);
        if (ReferenceEquals(previous, next))
        {
            return false;
        }

        Volatile.Write(ref _current, next);
        Changed?.Invoke(this, new SessionChangedEventArgs(previous, next));
        return true;
    }
}
