using Clicalo.Domain.Dimming;

namespace Clicalo.Application.Interaction;

/// <summary>
/// The single writer of the <see cref="InteractionState"/> (blueprint §3.2 rule 2, §6.4, ADR-0003): it belongs to the
/// Surfaces role of the UI thread that created it, and only that thread may <see cref="Dispatch"/>. Any thread may read
/// <see cref="Current"/>. ArchUnit keeps its mutating members inside the Surfaces role
/// (<c>OnlyTheSurfacesRoleWritesSessionAndInteraction</c>). It also answers how much each surface dims
/// (<see cref="Dim"/>), with <see cref="DimPolicy"/>.
/// </summary>
public sealed class InteractionStore
{
    private readonly int _ownerThread;
    private readonly TimeProvider _time;
    private InteractionState _current;

    /// <summary>Creates the store on the thread of the Surfaces role, which becomes its only writer.</summary>
    /// <param name="initial">The first state.</param>
    /// <param name="time">The clock of the dimming.</param>
    public InteractionStore(InteractionState initial, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(initial);
        ArgumentNullException.ThrowIfNull(time);
        _current = initial;
        _time = time;
        _ownerThread = Environment.CurrentManagedThreadId;
    }

    /// <summary>Raised on the owner thread after every change.</summary>
    public event EventHandler<InteractionChangedEventArgs>? Changed;

    /// <summary>The current state; safe to read from any thread.</summary>
    public InteractionState Current => Volatile.Read(ref _current);

    /// <summary>The clock of the dimming.</summary>
    public TimeProvider Clock => _time;

    /// <summary>Whether the calling thread is the store's only writer.</summary>
    public bool CheckAccess() => Environment.CurrentManagedThreadId == _ownerThread;

    /// <summary>Applies <paramref name="action"/> through <see cref="InteractionReducer"/>.</summary>
    /// <param name="action">The intention.</param>
    /// <returns>Whether the state changed.</returns>
    /// <exception cref="InvalidOperationException">Called from a thread other than the Surfaces role (a defect).</exception>
    public bool Dispatch(InteractionAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (!CheckAccess())
        {
            throw new InvalidOperationException(
                "InteractionStore is written only by the Surfaces role, on the thread that created it (blueprint §3.2)."
            );
        }

        var previous = _current;
        var next = InteractionReducer.Reduce(previous, action, _time.GetUtcNow());
        if (ReferenceEquals(previous, next))
        {
            return false;
        }

        Volatile.Write(ref _current, next);
        Changed?.Invoke(this, new InteractionChangedEventArgs(previous, next));
        return true;
    }

    /// <summary>
    /// How <paramref name="surface"/> dims now (GEN-009, docs/04): <see cref="DimPolicy"/> with the settings, the
    /// pointer, the last leave and every exception of the state.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="settings">The opacity and the dimming of General.</param>
    /// <param name="reduceMotion">Reduce motion is on (TEM-006).</param>
    /// <param name="highContrast">A contrast theme is on (PAN-003).</param>
    public DimDecision Dim(
        DimSurface surface,
        DimSettings settings,
        bool reduceMotion,
        bool highContrast
    )
    {
        var state = Current;
        return DimPolicy.Evaluate(
            new DimInputs(
                settings.AutoDim,
                settings.Opacity,
                settings.DimTo,
                surface,
                state.PointerInside,
                state.LastLeave,
                state.Exceptions,
                reduceMotion,
                highContrast,
                _time.GetUtcNow()
            )
        );
    }
}
