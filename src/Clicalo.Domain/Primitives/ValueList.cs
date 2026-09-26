using System.Collections;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace Clicalo.Domain.Primitives;

/// <summary>
/// An immutable list with <b>structural</b> equality, so records that contain it compare by value (blueprint §6.1).
/// The default value is the empty list. Build it with a collection expression (<c>[a, b]</c>) or from an
/// <see cref="ImmutableArray{T}"/>.
/// </summary>
/// <typeparam name="T">Element type; its own equality is used.</typeparam>
[CollectionBuilder(typeof(ValueListBuilder), nameof(ValueListBuilder.Create))]
public readonly struct ValueList<T> : IEquatable<ValueList<T>>, IReadOnlyList<T>
{
    private readonly ImmutableArray<T> _items;

    /// <summary>Wraps <paramref name="items"/>; a default array is the empty list.</summary>
    /// <param name="items">The elements, in order.</param>
    public ValueList(ImmutableArray<T> items) => _items = items;

    /// <summary>The elements, never a default array.</summary>
    public ImmutableArray<T> Items => _items.IsDefault ? [] : _items;

    /// <inheritdoc />
    public int Count => Items.Length;

    /// <summary>Whether the list has no elements.</summary>
    public bool IsEmpty => Count == 0;

    /// <inheritdoc />
    public T this[int index] => Items[index];

    /// <summary>Whether two lists hold equal elements in the same order.</summary>
    /// <param name="left">First list.</param>
    /// <param name="right">Second list.</param>
    public static bool operator ==(ValueList<T> left, ValueList<T> right) => left.Equals(right);

    /// <summary>Whether two lists differ.</summary>
    /// <param name="left">First list.</param>
    /// <param name="right">Second list.</param>
    public static bool operator !=(ValueList<T> left, ValueList<T> right) => !left.Equals(right);

    /// <inheritdoc />
    public bool Equals(ValueList<T> other) =>
        Items.AsSpan().SequenceEqual(other.Items.AsSpan(), EqualityComparer<T>.Default);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ValueList<T> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var item in Items)
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }

    /// <summary>Allocation-free enumerator.</summary>
    public ImmutableArray<T>.Enumerator GetEnumerator() => Items.GetEnumerator();

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => ((IEnumerable<T>)Items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)Items).GetEnumerator();
}
