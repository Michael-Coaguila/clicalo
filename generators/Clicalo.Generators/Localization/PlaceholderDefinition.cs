namespace Clicalo.Generators.Localization;

/// <summary>A named placeholder of the closed vocabulary in <c>placeholders.json</c>.</summary>
internal sealed class PlaceholderDefinition(
    string name,
    PlaceholderType type,
    string description,
    int order
)
{
    public string Name { get; } = name;

    public PlaceholderType Type { get; } = type;

    /// <summary>Documentation for translators and for the generated XML docs (Spanish).</summary>
    public string Description { get; } = description;

    /// <summary>Declaration order; generated parameters follow it so that editing a text never reorders them.</summary>
    public int Order { get; } = order;

    public bool IsNumeric => Type is PlaceholderType.Integer or PlaceholderType.Number;
}
