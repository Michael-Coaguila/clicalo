using System.Collections.Frozen;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Library;

/// <summary>
/// Frozen lookups of a <see cref="ShortcutLibrary"/>, built lazily on first use. The library is immutable, so two
/// threads that race to build it build equal indexes and either one wins.
/// </summary>
internal sealed class ShortcutLibraryIndex
{
    private ShortcutLibraryIndex(
        FrozenDictionary<ShortcutId, ShortcutLocation> shortcuts,
        FrozenDictionary<ProfileId, int> profiles
    )
    {
        Shortcuts = shortcuts;
        Profiles = profiles;
    }

    /// <summary>Where each shortcut lives.</summary>
    public FrozenDictionary<ShortcutId, ShortcutLocation> Shortcuts { get; }

    /// <summary>The position of each profile.</summary>
    public FrozenDictionary<ProfileId, int> Profiles { get; }

    /// <summary>Builds the index of the given lists (ids are unique, invariant I1).</summary>
    public static ShortcutLibraryIndex Build(
        ValueList<Shortcut> alwaysVisible,
        ValueList<Profile> profiles
    )
    {
        var shortcuts = new Dictionary<ShortcutId, ShortcutLocation>();
        var always = new ListRef.AlwaysVisible();
        for (var i = 0; i < alwaysVisible.Count; i++)
        {
            shortcuts.TryAdd(alwaysVisible[i].Id, new ShortcutLocation(always, i));
        }

        var positions = new Dictionary<ProfileId, int>();
        for (var p = 0; p < profiles.Count; p++)
        {
            var profile = profiles[p];
            positions.TryAdd(profile.Id, p);
            var list = new ListRef.InProfile(profile.Id);
            for (var i = 0; i < profile.Shortcuts.Count; i++)
            {
                shortcuts.TryAdd(profile.Shortcuts[i].Id, new ShortcutLocation(list, i));
            }
        }

        return new ShortcutLibraryIndex(
            shortcuts.ToFrozenDictionary(),
            positions.ToFrozenDictionary()
        );
    }
}
