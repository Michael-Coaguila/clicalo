namespace Clicalo.Domain.Execution;

/// <summary>Which part of an activation arrives.</summary>
public enum ActivationPhase
{
    /// <summary>A contact started on the tile (a Hold presses here, after the minimum contact).</summary>
    ContactStarted,

    /// <summary>A contact ended on the tile (a tap is decided here, TAC-002).</summary>
    ContactEnded,

    /// <summary>An invocation without contact or duration: no touch filter; a Hold behaves as a toggle (EJE-005).</summary>
    Invoke,
}
