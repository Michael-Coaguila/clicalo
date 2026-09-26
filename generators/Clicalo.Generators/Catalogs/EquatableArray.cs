using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Clicalo.Generators.Catalogs;

/// <summary>
/// Immutable array with structural equality, so collected inputs of the incremental pipeline compare by content
/// instead of by reference and unchanged outputs are cached.
/// </summary>
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    private readonly ImmutableArray<T> _items;

    public EquatableArray(ImmutableArray<T> items) => _items = items;

    public int Length => _items.IsDefault ? 0 : _items.Length;

    public bool Equals(EquatableArray<T> other)
    {
        if (Length != other.Length)
        {
            return false;
        }

        for (var i = 0; i < Length; i++)
        {
            if (!_items[i].Equals(other._items[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        var hash = 17;
        foreach (var item in this)
        {
            hash = unchecked((hash * 31) + item.GetHashCode());
        }

        return hash;
    }

    public ImmutableArray<T>.Enumerator GetEnumerator() =>
        (_items.IsDefault ? ImmutableArray<T>.Empty : _items).GetEnumerator();

    IEnumerator<T> IEnumerable<T>.GetEnumerator() =>
        ((IEnumerable<T>)(_items.IsDefault ? ImmutableArray<T>.Empty : _items)).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<T>)this).GetEnumerator();

    public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right) =>
        left.Equals(right);

    public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right) =>
        !left.Equals(right);
}
