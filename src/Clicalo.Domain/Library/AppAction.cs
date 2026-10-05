namespace Clicalo.Domain.Library;

/// <summary>Start an app, a Store app or a document, never elevated and never through an interpreter (EJE-011).</summary>
/// <param name="Target">What to start.</param>
public sealed record AppAction(AppTarget Target) : ShortcutAction
{
    /// <inheritdoc />
    public override ActionKind Kind => ActionKind.App;
}
