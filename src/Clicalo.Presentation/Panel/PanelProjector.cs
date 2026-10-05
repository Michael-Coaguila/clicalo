using System.Collections.Immutable;
using Clicalo.Application.Coordinators;
using Clicalo.Domain.Frequents;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.Panel;

/// <summary>
/// The pure projection of the panel (blueprint §8.2): the shortcuts of one profile become tiles, in display order,
/// with their name in the interface language, the line of keys they send and the injection mode of their profile
/// (D24); with the library, the Always visible row and every profile join. Frequents (FRE-001) is projected from the
/// entries <see cref="FrequentsProjection"/> composes. View models only apply its result; they decide no product rule.
/// </summary>
public static class PanelProjector
{
    /// <summary>Projects <paramref name="profile"/> alone.</summary>
    /// <param name="profile">The profile in view.</param>
    /// <param name="language">The interface language.</param>
    /// <param name="fallback">The language used when a name lacks <paramref name="language"/> (the default one).</param>
    /// <param name="keys">The key line of a shortcut (CUA-007); <see langword="null"/> shows none.</param>
    public static PanelModel Project(
        Profile profile,
        LangCode language,
        LangCode fallback,
        Func<Shortcut, TileKeyLine>? keys = null
    )
    {
        ArgumentNullException.ThrowIfNull(profile);
        return new PanelModel(
            profile.Id,
            TilesOf(profile.Shortcuts, profile.Id, profile.Injection, language, fallback, keys),
            ProfileName: profile.Name.Get(language, fallback),
            ProfileIcon: profile.Icon
        );
    }

    /// <summary>
    /// Projects the profile <paramref name="view"/> of <paramref name="library"/> with the Always visible row and
    /// every profile (SEL-003). The row runs with the mode of the profile in view (D24) and shows no key line (its tiles are\n    /// too small, FIJ-002); a profile that no longer
    /// exists shows General (PER-008).
    /// </summary>
    /// <param name="library">The library.</param>
    /// <param name="view">The profile in view.</param>
    /// <param name="language">The interface language.</param>
    /// <param name="fallback">The language used when a name lacks <paramref name="language"/>.</param>
    /// <param name="keys">The key line of a shortcut (CUA-007); <see langword="null"/> shows none.</param>
    public static PanelModel Project(
        ShortcutLibrary library,
        ProfileId view,
        LangCode language,
        LangCode fallback,
        Func<Shortcut, TileKeyLine>? keys = null
    )
    {
        ArgumentNullException.ThrowIfNull(library);
        var profile = library.Profiles.FirstOrDefault(p => p.Id == view) ?? library.General;
        var profiles = library
            .Profiles.Select(p => new PickerEntry(p.Id, p.Name.Get(language, fallback), p.Icon))
            .ToImmutableArray();
        return Project(profile, language, fallback, keys) with
        {
            Strip = TilesOf(
                library.AlwaysVisible,
                null,
                profile.Injection,
                language,
                fallback,
                keys: null
            ),
            Profiles = profiles,
        };
    }

    /// <summary>
    /// Projects Frequents (FRE-001, FRE-003): the tiles of <paramref name="entries"/>, each running with the profile of
    /// its list and showing that profile (or «Siempre visible») under its name; the selector and the profile grid keep
    /// the return profile (PER-004).
    /// </summary>
    /// <param name="library">The library.</param>
    /// <param name="entries">The tiles of Frequents, in order.</param>
    /// <param name="returnProfile">The profile the profile button goes back to.</param>
    /// <param name="alwaysVisibleName">«Siempre visible» in the interface language.</param>
    /// <param name="language">The interface language.</param>
    /// <param name="fallback">The language used when a name lacks <paramref name="language"/>.</param>
    public static PanelModel ProjectFrequents(
        ShortcutLibrary library,
        IReadOnlyList<FrequentEntry> entries,
        ProfileId returnProfile,
        string alwaysVisibleName,
        LangCode language,
        LangCode fallback
    )
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(alwaysVisibleName);
        var frame = Project(library, returnProfile, language, fallback);
        var tiles = ImmutableArray.CreateBuilder<TileModel>(entries.Count);
        foreach (var entry in entries)
        {
            var origin =
                entry.Location.List is ListRef.InProfile list
                && library.TryGetProfile(list.Id, out var owner)
                    ? owner
                    : null;
            var originName = origin?.Name.Get(language, fallback) ?? alwaysVisibleName;
            tiles.Add(
                TileOf(
                    entry.Shortcut,
                    origin?.Id,
                    (origin ?? library.General).Injection,
                    language,
                    fallback,
                    new TileKeyLine(originName, originName)
                )
            );
        }

        return frame with
        {
            Tiles = tiles.MoveToImmutable(),
        };
    }

    /// <summary>How the tile of <paramref name="action"/> reacts to the finger.</summary>
    /// <param name="action">The action of the shortcut.</param>
    public static TileBehavior BehaviorOf(ShortcutAction action) =>
        action switch
        {
            HoldAction => TileBehavior.Hold,
            ToggleAction => TileBehavior.Toggle,
            _ => TileBehavior.Tap,
        };

    private static ImmutableArray<TileModel> TilesOf(
        ValueList<Shortcut> shortcuts,
        ProfileId? origin,
        InjectionMode injection,
        LangCode language,
        LangCode fallback,
        Func<Shortcut, TileKeyLine>? keys
    )
    {
        var tiles = ImmutableArray.CreateBuilder<TileModel>(shortcuts.Count);
        foreach (var shortcut in shortcuts)
        {
            tiles.Add(
                TileOf(
                    shortcut,
                    origin,
                    injection,
                    language,
                    fallback,
                    keys?.Invoke(shortcut) ?? TileKeyLine.None
                )
            );
        }

        return tiles.MoveToImmutable();
    }

    private static TileModel TileOf(
        Shortcut shortcut,
        ProfileId? origin,
        InjectionMode injection,
        LangCode language,
        LangCode fallback,
        TileKeyLine keys
    ) =>
        new(
            shortcut.Id,
            shortcut.Name.Get(language, fallback),
            BehaviorOf(shortcut.Action),
            new TileBinding(shortcut, origin, injection),
            shortcut.Icon,
            shortcut.Category,
            keys.Line,
            keys.Spoken
        );
}
