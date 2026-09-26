namespace Clicalo.Domain.Touch;

/// <summary>Why a hold ended (EJE-004).</summary>
public enum HoldEndReason
{
    /// <summary>Not a hold end.</summary>
    None,

    /// <summary>The finger lifted.</summary>
    Lifted,

    /// <summary>The system cancelled the contact.</summary>
    Canceled,

    /// <summary>The contact left the extra hit area of its target.</summary>
    LeftTarget,

    /// <summary>The recognizer was reset (surface hidden, session locked, settings changed).</summary>
    Reset,
}
