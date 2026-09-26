namespace Clicalo.Domain.Execution;

/// <summary>Why the panel refuses to run a shortcut.</summary>
public enum RefusalReason
{
    /// <summary>The shortcut is incomplete (EJE-015).</summary>
    Incomplete,

    /// <summary>The combination is blocked in the panel, such as Ctrl+Alt+Del (EJE-014).</summary>
    BlockedCombo,
}
