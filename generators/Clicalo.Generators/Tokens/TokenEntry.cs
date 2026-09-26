namespace Clicalo.Generators.Tokens;

/// <summary>
/// A color token as declared, before resolution: its text (a CSS color, or the key of another token of the same
/// theme when it is an alias) and where it was written.
/// </summary>
internal sealed class TokenEntry(string key, string text, DataPosition position, string? note)
{
    public string Key { get; } = key;

    public string Text { get; set; } = text;

    public DataPosition Position { get; set; } = position;

    /// <summary>Correction note, when a documented correction replaced the original value.</summary>
    public string? Note { get; set; } = note;

    /// <summary>True for <c>border</c>, whose text is the shorthand <c>&lt;n&gt;px solid &lt;color&gt;</c>.</summary>
    public bool IsBorderShorthand { get; init; }

    public bool IsAlias => JsonShape.IsIdentifier(Text);
}
