using System.Collections.Immutable;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Migration.V1;

/// <summary>
/// The repeated combinations a v1 import creates (MIG-008): REP-002 applied as is to the converted library (Always
/// visible is empty after an import, so only «same list» and «same name in ES and EN» can apply). Their keys go to
/// «It's fine» (<c>dupIgnored</c>) and to the report, so the import does not flood the panel with warnings.
/// </summary>
internal static class V1DuplicateScan
{
    /// <summary>The canonical keys that REP-002 would flag, in order of first appearance, and one report line each.</summary>
    /// <param name="library">The converted library.</param>
    public static (ValueList<CanonicalChord> Keys, ImmutableArray<MigrationNote> Notes) Find(
        ShortcutLibrary library
    )
    {
        ArgumentNullException.ThrowIfNull(library);
        var order = new List<CanonicalChord>();
        var groups = new Dictionary<CanonicalChord, List<(Profile Profile, Shortcut Shortcut)>>();
        foreach (var profile in library.Profiles)
        {
            foreach (var shortcut in profile.Shortcuts)
            {
                // REP-001: only Tap, Hold and Toggle with at least one key have a key.
                var chord = shortcut.Action switch
                {
                    TapAction tap => tap.Chord,
                    HoldAction hold => hold.Chord,
                    ToggleAction toggle => toggle.Chord,
                    _ => null,
                };
                if (chord is null || chord.IsEmpty || !CanonicalChord.TryFrom(chord, out var key))
                {
                    continue;
                }

                if (!groups.TryGetValue(key, out var members))
                {
                    members = [];
                    groups.Add(key, members);
                    order.Add(key);
                }

                members.Add((profile, shortcut));
            }
        }

        var keys = ImmutableArray.CreateBuilder<CanonicalChord>();
        var notes = ImmutableArray.CreateBuilder<MigrationNote>();
        foreach (var key in order)
        {
            if (FirstRepeated(groups[key]) is not { } first)
            {
                continue;
            }

            keys.Add(key);
            notes.Add(
                new MigrationNote(
                    MigrationNoteKind.DuplicateIgnored,
                    first.Profile.Name.Get(LangCode.Es, LangCode.En),
                    first.Shortcut.Name.Get(LangCode.Es, LangCode.En),
                    key.ToStableString()
                )
            );
        }

        return (new ValueList<CanonicalChord>(keys.ToImmutable()), notes.ToImmutable());
    }

    /// <summary>
    /// The earlier member of the first pair REP-002 flags (same list, or same name in every language), in one pass.
    /// </summary>
    private static (Profile Profile, Shortcut Shortcut)? FirstRepeated(
        List<(Profile Profile, Shortcut Shortcut)> members
    )
    {
        var byList = new Dictionary<ProfileId, int>();
        var byName = new Dictionary<LocalizedText, int>();
        for (var i = 0; i < members.Count; i++)
        {
            if (byList.TryGetValue(members[i].Profile.Id, out var sameList))
            {
                return members[sameList];
            }

            if (byName.TryGetValue(members[i].Shortcut.Name, out var sameName))
            {
                return members[sameName];
            }

            byList.Add(members[i].Profile.Id, i);
            byName.Add(members[i].Shortcut.Name, i);
        }

        return null;
    }
}
