namespace Clicalo.Generators.Localization;

/// <summary>A <c>{name}</c> occurrence in a text, with the index of its opening brace in the decoded string.</summary>
internal readonly struct TemplatePlaceholder(string name, int index)
{
    public string Name { get; } = name;

    public int Index { get; } = index;
}
