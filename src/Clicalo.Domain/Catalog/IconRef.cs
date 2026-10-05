namespace Clicalo.Domain.Catalog;

/// <summary>An icon of the embedded Material Symbols font (<c>format_bold</c>), from <c>data/catalogs/icons.json</c>.</summary>
/// <param name="Name">The Material Symbols name, exactly as persisted.</param>
public readonly record struct IconRef(string Name)
{
    /// <inheritdoc />
    public override string ToString() => Name ?? string.Empty;
}
