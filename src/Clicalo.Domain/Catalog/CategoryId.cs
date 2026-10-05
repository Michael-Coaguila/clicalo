namespace Clicalo.Domain.Catalog;

/// <summary>
/// Colour category of a shortcut (<c>edit</c>, <c>hist</c>, <c>file</c>, <c>sel</c>, <c>win</c>, <c>voice</c>,
/// <c>nav</c>, <c>fmt</c>, <c>web</c>, <c>text</c>), from <c>data/catalogs/categories.json</c>.
/// </summary>
/// <param name="Value">The category identifier, exactly as persisted.</param>
public readonly record struct CategoryId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}
