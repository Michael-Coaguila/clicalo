namespace Clicalo.Domain.Touch;

/// <summary>Where a contact is in its life, from its <c>WM_POINTER*</c> message and flags.</summary>
public enum PointerPhase
{
    /// <summary>The contact starts (<c>WM_POINTERDOWN</c>, <c>POINTER_FLAG_DOWN</c>).</summary>
    Down,

    /// <summary>The contact moves or reports new data (<c>WM_POINTERUPDATE</c>).</summary>
    Move,

    /// <summary>The contact ends normally (<c>WM_POINTERUP</c>, <c>POINTER_FLAG_UP</c>).</summary>
    Up,

    /// <summary>
    /// The contact ends without a normal lift (<c>POINTER_FLAG_CANCELED</c>, <c>WM_POINTERCAPTURECHANGED</c>, the
    /// window hides, the session locks): a hold is released (EJE-004) and nothing else is executed.
    /// </summary>
    Cancel,
}
