namespace Clicalo.Domain.Primitives;

/// <summary>Builds <see cref="ValueList{T}"/> from collection expressions and sequences.</summary>
public static class ValueListBuilder
{
    /// <summary>Creates a list with a copy of <paramref name="items"/>.</summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="items">The elements, in order.</param>
    public static ValueList<T> Create<T>(ReadOnlySpan<T> items) => new([.. items]);

    /// <summary>Creates a list from a sequence.</summary>
    /// <typeparam name="T">Element type.</typeparam>
    /// <param name="items">The elements, in order.</param>
    public static ValueList<T> From<T>(IEnumerable<T> items) => new([.. items]);
}
