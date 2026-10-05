namespace Clicalo.Domain.Library;

/// <summary>Where to insert in a list.</summary>
/// <param name="Index">Zero-based index, or <see langword="null"/> for the end.</param>
public readonly record struct ListPosition(int? Index)
{
    /// <summary>The end of the list.</summary>
    public static ListPosition End { get; } = new(null);

    /// <summary>Before the element at <paramref name="index"/>.</summary>
    /// <param name="index">Zero-based index.</param>
    public static ListPosition At(int index) => new(index);
}
