using System.Collections.Frozen;
using System.Reflection;
using System.Windows;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Controls;

/// <summary>
/// Resource key of the frozen brush of a category color (TEM-003): the tint (icon, active border and badge text of a
/// tile) or the wash (background of an active, held, armed or flashing tile). Written next to the
/// <see cref="ThemeBrushKey"/> brushes by <see cref="ThemeScope"/> and <see cref="Theming.ThemeService"/>, so a
/// theme change repaints the categories in place.
/// </summary>
/// <remarks>There is one shared instance per category and kind; keys compare by value.</remarks>
public sealed class CategoryBrushKey : ResourceKey, IEquatable<CategoryBrushKey>
{
    private static readonly FrozenDictionary<CategoryToken, CategoryBrushKey> Tints =
        Enum.GetValues<CategoryToken>()
            .ToFrozenDictionary(
                category => category,
                category => new CategoryBrushKey(category, false)
            );

    private static readonly FrozenDictionary<CategoryToken, CategoryBrushKey> Washes =
        Enum.GetValues<CategoryToken>()
            .ToFrozenDictionary(
                category => category,
                category => new CategoryBrushKey(category, true)
            );

    private CategoryBrushKey(CategoryToken category, bool isWash)
    {
        Category = category;
        IsWash = isWash;
    }

    /// <summary>The category whose color this key names.</summary>
    public CategoryToken Category { get; }

    /// <summary>True for the wash (active background), false for the tint.</summary>
    public bool IsWash { get; }

    /// <summary>Every key: the tint and the wash of each category.</summary>
    public static IEnumerable<CategoryBrushKey> All => Tints.Values.Concat(Washes.Values);

    /// <inheritdoc />
    public override Assembly Assembly => typeof(CategoryBrushKey).Assembly;

    /// <summary>The key of the tint of <paramref name="category"/>.</summary>
    public static CategoryBrushKey Tint(CategoryToken category) => Lookup(Tints, category);

    /// <summary>The key of the wash of <paramref name="category"/>.</summary>
    public static CategoryBrushKey Wash(CategoryToken category) => Lookup(Washes, category);

    /// <inheritdoc />
    public bool Equals(CategoryBrushKey? other) =>
        other is not null && other.Category == Category && other.IsWash == IsWash;

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as CategoryBrushKey);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Category, IsWash);

    /// <summary>The category and kind, for diagnostics.</summary>
    public override string ToString() => (IsWash ? "Wash." : "Tint.") + Category;

    private static CategoryBrushKey Lookup(
        FrozenDictionary<CategoryToken, CategoryBrushKey> keys,
        CategoryToken category
    ) =>
        keys.TryGetValue(category, out var key)
            ? key
            : throw new ArgumentOutOfRangeException(nameof(category), category, message: null);
}
