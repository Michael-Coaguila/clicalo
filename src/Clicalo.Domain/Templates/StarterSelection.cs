using System.Collections.Immutable;

namespace Clicalo.Domain.Templates;

/// <summary>
/// The options marked in the welcome step «Which apps do you use most?» (BIE-006): the ids of
/// <see cref="StarterKit.Options"/>. Nothing marked starts empty, with General and Always visible only (user decision
/// D2); skipping the welcome is <see cref="StarterKit.DefaultSelection"/>. Immutable, with value equality.
/// </summary>
public sealed class StarterSelection : IEquatable<StarterSelection>
{
    private StarterSelection(ImmutableSortedSet<string> chosen) => Chosen = chosen;

    /// <summary>Nothing marked: General and Always visible, both empty.</summary>
    public static StarterSelection Empty { get; } = new([]);

    /// <summary>The marked option ids.</summary>
    public ImmutableSortedSet<string> Chosen { get; }

    /// <summary>A selection of <paramref name="optionIds"/>; repeated ids count once.</summary>
    /// <param name="optionIds">Ids of options of the kit; an id the kit does not have installs nothing.</param>
    public static StarterSelection Of(IEnumerable<string> optionIds)
    {
        ArgumentNullException.ThrowIfNull(optionIds);
        return new StarterSelection(optionIds.ToImmutableSortedSet(StringComparer.Ordinal));
    }

    /// <summary>Whether <paramref name="optionId"/> is marked.</summary>
    /// <param name="optionId">An option id.</param>
    public bool IsChosen(string optionId) => Chosen.Contains(optionId);

    /// <summary>The same selection with <paramref name="optionId"/> marked or unmarked (the chips of BIE-006).</summary>
    /// <param name="optionId">An option id.</param>
    public StarterSelection Toggle(string optionId)
    {
        var without = Chosen.Remove(optionId);
        return new(without.Count == Chosen.Count ? Chosen.Add(optionId) : without);
    }

    /// <inheritdoc />
    public bool Equals(StarterSelection? other) =>
        other is not null && Chosen.SetEquals(other.Chosen);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as StarterSelection);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var id in Chosen)
        {
            hash.Add(id, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }
}
