using System.Collections.Generic;

namespace Clicalo.Generators.Localization;

/// <summary>One entry of a strings file: a plain text or one plural form of a family.</summary>
internal sealed class StringForm(
    string key,
    string baseKey,
    string? category,
    string text,
    IReadOnlyList<TemplatePlaceholder> placeholders,
    int keyLine,
    int keyColumn
)
{
    /// <summary>Physical key as written in the file (<c>comboN_one</c>).</summary>
    public string Key { get; } = key;

    /// <summary>Key without plural suffix (<c>comboN</c>).</summary>
    public string BaseKey { get; } = baseKey;

    /// <summary>CLDR category of a plural form, or <c>null</c> for a plain text.</summary>
    public string? Category { get; } = category;

    public string Text { get; } = text;

    public IReadOnlyList<TemplatePlaceholder> Placeholders { get; } = placeholders;

    public int KeyLine { get; } = keyLine;

    public int KeyColumn { get; } = keyColumn;
}
