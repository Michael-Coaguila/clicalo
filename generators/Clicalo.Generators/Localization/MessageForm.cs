namespace Clicalo.Generators.Localization;

/// <summary>A text of the default language, used to document a generated member.</summary>
internal sealed class MessageForm(string? category, string text)
{
    /// <summary>CLDR category of a plural form, or <c>null</c> for a plain text.</summary>
    public string? Category { get; } = category;

    public string Text { get; } = text;
}
