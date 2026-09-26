namespace Clicalo.Domain.Library;

/// <summary>
/// What a shortcut does: a closed hierarchy (the constructor is <c>private protected</c>), one sealed record per
/// <see cref="ActionKind"/> (blueprint §6.2). An incomplete action is still valid: it can be saved and undone, and the
/// engine refuses to run it (ATJ-009, EJE-015, <see cref="ShortcutCompleteness"/>).
/// </summary>
public abstract record ShortcutAction
{
    private protected ShortcutAction() { }

    /// <summary>The kind of action.</summary>
    public abstract ActionKind Kind { get; }
}
