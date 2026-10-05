namespace Clicalo.Domain.Library;

/// <summary>
/// The address of a web action (EJE-011): a valid <c>http</c> or <c>https</c> address, or the raw text the user or an
/// imported file wrote, kept so nothing is lost and marked for review.
/// </summary>
public abstract record UrlTarget
{
    private UrlTarget() { }

    /// <summary>An absolute <c>http</c> or <c>https</c> address («ejemplo.com» is completed to https://).</summary>
    /// <param name="Address">The address.</param>
    public sealed record Valid(Uri Address) : UrlTarget;

    /// <summary>Text that is not a valid address yet (incomplete, EJE-015).</summary>
    /// <param name="Text">The text as written.</param>
    public sealed record Raw(string Text) : UrlTarget;
}
