using System.Collections.Immutable;
using Clicalo.Domain.Duplicates;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Migration.V1;

/// <summary>
/// The repeated combinations a v1 import creates (MIG-008): the panel's own repeated index (REP-002,
/// <see cref="DuplicateIndex"/>) applied to the converted library. Their keys go to «It's fine» (<c>dupIgnored</c>) and
/// to the report, so the import does not flood the panel with warnings and no key the panel would flag is left out.
/// </summary>
internal static class V1DuplicateScan
{
    /// <summary>The canonical keys that REP-002 flags, in order of first appearance, and one report line each.</summary>
    /// <param name="library">The converted library.</param>
    public static (ValueList<CanonicalChord> Keys, ImmutableArray<MigrationNote> Notes) Find(
        ShortcutLibrary library
    )
    {
        ArgumentNullException.ThrowIfNull(library);
        var index = DuplicateIndex.Build(library, DuplicatePolicy.Empty);
        var pending = new HashSet<CanonicalChord>(index.RepeatedCombinations);
        var first = new Dictionary<CanonicalChord, LocatedShortcut>();
        foreach (var located in library.EnumerateShortcuts())
        {
            if (
                pending.Count > 0
                && index.IsRepeated(located.Shortcut.Id)
                && DuplicateIndex.TryGetKey(located.Shortcut, out var key)
                && pending.Remove(key)
            )
            {
                first.Add(key, located);
            }
        }

        var notes = ImmutableArray.CreateBuilder<MigrationNote>(index.RepeatedCombinations.Length);
        foreach (var key in index.RepeatedCombinations)
        {
            var located = first[key];
            notes.Add(
                new MigrationNote(
                    MigrationNoteKind.DuplicateIgnored,
                    ProfileName(library, located.Location.List),
                    located.Shortcut.Name.Get(LangCode.Es, LangCode.En),
                    key.ToStableString()
                )
            );
        }

        return (new ValueList<CanonicalChord>(index.RepeatedCombinations), notes.MoveToImmutable());
    }

    private static string? ProfileName(ShortcutLibrary library, ListRef list) =>
        list is ListRef.InProfile { Id: var id }
        && library.Profiles.FirstOrDefault(p => p.Id == id) is { } profile
            ? profile.Name.Get(LangCode.Es, LangCode.En)
            : null;
}
