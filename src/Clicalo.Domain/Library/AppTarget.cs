namespace Clicalo.Domain.Library;

/// <summary>
/// What an app action starts (EJE-011, LOG-008): never through a command interpreter. Text that is not a safe target
/// yet (for example a command with interpreter arguments in an imported file) is kept as <see cref="Raw"/> and marked
/// for review, never run.
/// </summary>
public abstract record AppTarget
{
    private AppTarget() { }

    /// <summary>An executable, with arguments passed as they are (no interpreter).</summary>
    /// <param name="Path">Full path or executable name.</param>
    /// <param name="Arguments">Arguments; empty for none.</param>
    public sealed record Executable(string Path, string Arguments) : AppTarget;

    /// <summary>A Store app by its application user model id.</summary>
    /// <param name="AppUserModelId">The AUMID.</param>
    public sealed record StoreApp(string AppUserModelId) : AppTarget;

    /// <summary>A document opened with its default app.</summary>
    /// <param name="Path">Full path.</param>
    public sealed record Document(string Path) : AppTarget;

    /// <summary>Text that cannot be started safely as it is (incomplete, EJE-015).</summary>
    /// <param name="Text">The text as written.</param>
    public sealed record Raw(string Text) : AppTarget;
}
