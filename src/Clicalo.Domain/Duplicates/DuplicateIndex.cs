using System.Collections.Frozen;
using System.Collections.Immutable;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Duplicates;

/// <summary>
/// The repeated shortcuts of a library (REP-001 to REP-005), indexed by canonical key so it scales to thousands of
/// shortcuts without comparing all against all (REP-007). Immutable; build a new one when the library or the policy
/// changes.
/// </summary>
/// <remarks>
/// Only Tap, Hold and Toggle with at least one key have a key (REP-001). Two different shortcuts with the same key
/// are repeated when one of them is in Always visible, when they share a list, or when they have the same name in
/// Spanish and in English (REP-002); keys marked «It's fine» (<see cref="DuplicatePolicy.Ignored"/>) are never flagged.
/// The same combination in two profiles with different names is not a repetition: each app reads it its own way.
/// </remarks>
public sealed class DuplicateIndex
{
    private readonly FrozenDictionary<ShortcutId, CanonicalChord> _keyOf;
    private readonly FrozenDictionary<CanonicalChord, ImmutableArray<LocatedShortcut>> _repeated;

    private DuplicateIndex(
        FrozenDictionary<ShortcutId, CanonicalChord> keyOf,
        FrozenDictionary<CanonicalChord, ImmutableArray<LocatedShortcut>> repeated,
        ImmutableArray<CanonicalChord> order
    )
    {
        _keyOf = keyOf;
        _repeated = repeated;
        RepeatedCombinations = order;
    }

    /// <summary>The distinct repeated combinations, in document order of their first appearance (REP-003).</summary>
    public ImmutableArray<CanonicalChord> RepeatedCombinations { get; }

    /// <summary>How many distinct combinations are repeated: the count of the «Shortcuts» menu and chip (REP-003).</summary>
    public int RepeatedCombinationCount => RepeatedCombinations.Length;

    /// <summary>
    /// The appearance the «[review]» chip opens: the first repeated combination, at its first appearance outside
    /// Always visible if there is one (REP-003); <see langword="null"/> when nothing is repeated.
    /// </summary>
    public ShortcutId? FirstToReview =>
        RepeatedCombinations.IsEmpty
            ? null
            : Preferred(_repeated[RepeatedCombinations[0]], except: null);

    /// <summary>Indexes <paramref name="library"/>.</summary>
    /// <param name="library">The shortcuts.</param>
    /// <param name="policy">The keys marked «It's fine».</param>
    public static DuplicateIndex Build(ShortcutLibrary library, DuplicatePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(policy);
        var ignored = new HashSet<CanonicalChord>(policy.Ignored);
        var keyOf = new Dictionary<ShortcutId, CanonicalChord>();
        var groups = new Dictionary<CanonicalChord, List<LocatedShortcut>>();
        var order = new List<CanonicalChord>();
        foreach (var located in library.EnumerateShortcuts())
        {
            if (!TryGetKey(located.Shortcut, out var key))
            {
                continue;
            }

            keyOf[located.Shortcut.Id] = key;
            if (ignored.Contains(key))
            {
                continue;
            }

            if (!groups.TryGetValue(key, out var group))
            {
                group = [];
                groups.Add(key, group);
                order.Add(key);
            }

            group.Add(located);
        }

        var repeated = new Dictionary<CanonicalChord, ImmutableArray<LocatedShortcut>>();
        var repeatedOrder = ImmutableArray.CreateBuilder<CanonicalChord>();
        foreach (var key in order)
        {
            var appearances = RepeatedIn(groups[key]);
            if (!appearances.IsEmpty)
            {
                repeated.Add(key, appearances);
                repeatedOrder.Add(key);
            }
        }

        return new DuplicateIndex(
            keyOf.ToFrozenDictionary(),
            repeated.ToFrozenDictionary(),
            repeatedOrder.ToImmutable()
        );
    }

    /// <summary>
    /// The canonical key of a shortcut: only Tap, Hold and Toggle with at least one key have one (REP-001).
    /// </summary>
    /// <param name="shortcut">The shortcut.</param>
    /// <param name="key">Its key.</param>
    public static bool TryGetKey(Shortcut shortcut, out CanonicalChord key)
    {
        ArgumentNullException.ThrowIfNull(shortcut);
        var chord = shortcut.Action switch
        {
            TapAction tap => tap.Chord,
            HoldAction hold => hold.Chord,
            ToggleAction toggle => toggle.Chord,
            _ => null,
        };
        if (chord is null)
        {
            key = default;
            return false;
        }

        return CanonicalChord.TryFrom(chord, out key);
    }

    /// <summary>Whether a shortcut is a repeated appearance (the ⚠ of REP-003).</summary>
    /// <param name="id">The shortcut.</param>
    public bool IsRepeated(ShortcutId id) =>
        _keyOf.TryGetValue(id, out var key)
        && _repeated.TryGetValue(key, out var appearances)
        && appearances.Any(a => a.Shortcut.Id == id);

    /// <summary>
    /// The repeated appearances of the combination of <paramref name="id"/>, in document order, only when it is one
    /// of them: what the editor's card counts and walks in a circle (REP-004).
    /// </summary>
    /// <param name="id">The shortcut open in the editor.</param>
    public ImmutableArray<LocatedShortcut> AppearancesOf(ShortcutId id) =>
        IsRepeated(id) ? _repeated[_keyOf[id]] : [];

    /// <summary>The advice of the expanded card for the combination of <paramref name="id"/> (REP-005).</summary>
    /// <param name="id">The shortcut open in the editor.</param>
    public DuplicateAdvice? AdviceFor(ShortcutId id)
    {
        var appearances = AppearancesOf(id);
        if (appearances.IsEmpty)
        {
            return null;
        }

        if (appearances.Any(a => a.Location.List is ListRef.AlwaysVisible))
        {
            return DuplicateAdvice.AlwaysVisible;
        }

        var first = appearances[0].Shortcut.Name;
        return appearances.All(a => ShortcutNames.AreSame(first, a.Shortcut.Name))
            ? DuplicateAdvice.SameName
            : DuplicateAdvice.DifferentNames;
    }

    /// <summary>
    /// The appearance to open after deleting <paramref name="id"/> from the card: another one of its repeated
    /// appearances, preferably outside Always visible (REP-005).
    /// </summary>
    /// <param name="id">The appearance being deleted.</param>
    public ShortcutId? NextAfterDeleting(ShortcutId id)
    {
        var appearances = AppearancesOf(id);
        return appearances.IsEmpty ? null : Preferred(appearances, except: id);
    }

    private static ShortcutId? Preferred(
        ImmutableArray<LocatedShortcut> appearances,
        ShortcutId? except
    )
    {
        ShortcutId? fallback = null;
        foreach (var appearance in appearances)
        {
            if (appearance.Shortcut.Id == except)
            {
                continue;
            }

            if (appearance.Location.List is not ListRef.AlwaysVisible)
            {
                return appearance.Shortcut.Id;
            }

            fallback ??= appearance.Shortcut.Id;
        }

        return fallback;
    }

    private static ImmutableArray<LocatedShortcut> RepeatedIn(List<LocatedShortcut> group)
    {
        if (group.Count < 2)
        {
            return [];
        }

        // With one appearance in Always visible, every appearance is repeated with it (rule a).
        if (group.Exists(a => a.Location.List is ListRef.AlwaysVisible))
        {
            return [.. group];
        }

        // Otherwise an appearance is repeated when another shares its list (b) or its name (c).
        var perList = group.CountBy(a => a.Location.List).ToDictionary();
        var perName = group.CountBy(a => ShortcutNames.KeyOf(a.Shortcut.Name)).ToDictionary();
        return
        [
            .. group.Where(a =>
                perList[a.Location.List] > 1 || perName[ShortcutNames.KeyOf(a.Shortcut.Name)] > 1
            ),
        ];
    }
}
