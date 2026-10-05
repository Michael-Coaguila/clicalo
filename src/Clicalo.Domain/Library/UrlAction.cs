namespace Clicalo.Domain.Library;

/// <summary>Open an address with the default browser, never elevated (EJE-011).</summary>
/// <param name="Target">The address.</param>
public sealed record UrlAction(UrlTarget Target) : ShortcutAction
{
    /// <inheritdoc />
    public override ActionKind Kind => ActionKind.Url;
}
