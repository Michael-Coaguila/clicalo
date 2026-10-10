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
/// <remarks>
/// The compatible mode follows D24 (<see cref="InheritedMode"/>): the tiles of a profile bound to an app are sent in
/// the mode of that profile; General, Always visible and Frequents have no app of their own, so they inherit the mode
/// of the profile of the app in front, which is the one that needs it.
/// </remarks>
public static class PanelProjector
{
    /// <summary>
    /// The injection mode of a tile (D24): the mode of its own profile when that profile is not General; otherwise
    /// the mode of the profile of the app in front, and General's own mode while that app has no profile.
    /// </summary>
    /// <param name="library">The library.</param>
    /// <param name="origin">The profile the shortcut is shown from; <see langword="null"/> for Always visible.</param>
    /// <param name="appMode">The mode of the profile of the app in front, or <see langword="null"/> without one.</param>
    public static InjectionMode InheritedMode(
        ShortcutLibrary library,
        Profile? origin,
        InjectionMode? appMode
    )
    {
        ArgumentNullException.ThrowIfNull(library);
        return origin is not null && origin.Id != library.General.Id
            ? origin.Injection
            : appMode ?? library.General.Injection;
    }

    /// <summary>
    /// The mode of the profile <paramref name="activeApp"/> resolved for the app in front (D24), or
    /// <see langword="null"/> when that app has no profile.
    /// </summary>
    /// <param name="library">The library.</param>
    /// <param name="activeApp">The profile of the app in front, if any.</param>
    public static InjectionMode? AppMode(ShortcutLibrary library, ProfileId? activeApp)
    {
        ArgumentNullException.ThrowIfNull(library);
        return activeApp is { } id && library.TryGetProfile(id, out var profile)
            ? profile.Injection
            : null;
    }

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
    /// every profile (SEL-003). General and the row inherit the mode of the profile of the app in front (D24,
    /// <see cref="InheritedMode"/>); the row shows no key line (its tiles are too small, FIJ-002); a profile that no
    /// longer exists shows General (PER-008).
    /// </summary>
    /// <param name="library">The library.</param>
    /// <param name="view">The profile in view.</param>
    /// <param name="language">The interface language.</param>
    /// <param name="fallback">The language used when a name lacks <paramref name="language"/>.</param>
    /// <param name="keys">The key line of a shortcut (CUA-007); <see langword="null"/> shows none.</param>
    /// <param name="appMode">The mode of the profile of the app in front (<see cref="AppMode"/>), if any.</param>
    public static PanelModel Project(
        ShortcutLibrary library,
        ProfileId view,
        LangCode language,
        LangCode fallback,
        Func<Shortcut, TileKeyLine>? keys = null,
        InjectionMode? appMode = null
    )
    {
        ArgumentNullException.ThrowIfNull(library);
        var profile = library.Profiles.FirstOrDefault(p => p.Id == view) ?? library.General;
        var profiles = library
            .Profiles.Select(p => new PickerEntry(p.Id, p.Name.Get(language, fallback), p.Icon))
            .ToImmutableArray();
        return new PanelModel(
            profile.Id,
            TilesOf(
                profile.Shortcuts,
                profile.Id,
                InheritedMode(library, profile, appMode),
                language,
                fallback,
                keys
            ),
            ProfileName: profile.Name.Get(language, fallback),
            ProfileIcon: profile.Icon
        )
        {
            Strip = TilesOf(
                library.AlwaysVisible,
                null,
                InheritedMode(library, null, appMode),
                language,
                fallback,
                keys: null
            ),
            Profiles = profiles,
        };
    }

    /// <summary>
    /// Projects Frequents (FRE-001, FRE-003): the tiles of <paramref name="entries"/>, each counted for the profile of
    /// its list and showing that profile (or «Siempre visible») under its name; the selector and the profile grid keep
    /// the return profile (PER-004). Frequents inherits the mode of the profile of the app in front (D24); without
    /// one, each tile keeps the mode of the profile it comes from.
    /// </summary>
    /// <param name="library">The library.</param>
    /// <param name="entries">The tiles of Frequents, in order.</param>
    /// <param name="returnProfile">The profile the profile button goes back to.</param>
    /// <param name="alwaysVisibleName">«Siempre visible» in the interface language.</param>
    /// <param name="language">The interface language.</param>
    /// <param name="fallback">The language used when a name lacks <paramref name="language"/>.</param>
    /// <param name="appMode">The mode of the profile of the app in front (<see cref="AppMode"/>), if any.</param>
    public static PanelModel ProjectFrequents(
        ShortcutLibrary library,
        IReadOnlyList<FrequentEntry> entries,
        ProfileId returnProfile,
        string alwaysVisibleName,
        LangCode language,
        LangCode fallback,
        InjectionMode? appMode = null
    )
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(alwaysVisibleName);
        var frame = Project(library, returnProfile, language, fallback, keys: null, appMode);
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
                    appMode ?? (origin ?? library.General).Injection,
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
            // The engine repeats a scroll while the finger rests on it, as a Hold (EJE-009).
            MouseAction mouse when MouseOps.RepeatsWhileHeld(mouse.Op) => TileBehavior.Hold,
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
