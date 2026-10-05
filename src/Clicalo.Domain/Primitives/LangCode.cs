namespace Clicalo.Domain.Primitives;

/// <summary>
/// A language of the product or of the target apps, as a lower-case BCP 47 tag (<c>es</c>, <c>en</c>). Kept open so a
/// third language needs no model change (IDI-006).
/// </summary>
/// <param name="Value">The tag.</param>
public readonly record struct LangCode(string Value)
{
    /// <summary>Spanish, the source language of the product.</summary>
    public static LangCode Es { get; } = new("es");

    /// <summary>English.</summary>
    public static LangCode En { get; } = new("en");

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}
