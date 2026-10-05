using System.Collections.Immutable;
using Clicalo.Application.Coordinators;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.Panel;

/// <summary>
/// The pure projection of the panel (blueprint §8.2): the shortcuts of one profile become tiles, in display order,
/// with their name in the interface language and the injection mode of their profile (D24); with the library, the
/// Always visible row and every profile join. View models only apply its result; they decide no product rule. The
/// full <c>PanelProjector</c> of M3 (Frequents, search, dimming) replaces it in <c>Application.Projections</c>.
/// </summary>
public static class PanelProjector
{
    /// <summary>Projects <paramref name="profile"/> alone.</summary>
    /// <param name="profile">The profile in view.</param>
    /// <param name="language">The interface language.</param>
    /// <param name="fallback">The language used when a name lacks <paramref name="language"/> (the default one).</param>
    public static PanelModel Project(Profile profile, LangCode language, LangCode fallback)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return new PanelModel(
            profile.Id,
            TilesOf(profile.Shortcuts, profile.Id, profile.Injection, language, fallback),
            ProfileName: profile.Name.Get(language, fallback),
            ProfileIcon: profile.Icon
        );
    }

    /// <summary>
    /// Projects the profile <paramref name="view"/> of <paramref name="library"/> with the Always visible row and
    /// every profile (SEL-003). The row runs with the mode of the profile in view (D24); a profile that no longer
    /// exists shows General (PER-008).
    /// </summary>
    /// <param name="library">The library.</param>
    /// <param name="view">The profile in view.</param>
    /// <param name="language">The interface language.</param>
    /// <param name="fallback">The language used when a name lacks <paramref name="language"/>.</param>
    public static PanelModel Project(
        ShortcutLibrary library,
        ProfileId view,
        LangCode language,
        LangCode fallback
    )
    {
        ArgumentNullException.ThrowIfNull(library);
        var profile = library.Profiles.FirstOrDefault(p => p.Id == view) ?? library.General;
        var profiles = library
            .Profiles.Select(p => new PickerEntry(p.Id, p.Name.Get(language, fallback), p.Icon))
            .ToImmutableArray();
        return Project(profile, language, fallback) with
        {
            Strip = TilesOf(library.AlwaysVisible, null, profile.Injection, language, fallback),
            Profiles = profiles,
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
        LangCode fallback
    )
    {
        var tiles = ImmutableArray.CreateBuilder<TileModel>(shortcuts.Count);
        foreach (var shortcut in shortcuts)
        {
            tiles.Add(
                new TileModel(
                    shortcut.Id,
                    shortcut.Name.Get(language, fallback),
                    BehaviorOf(shortcut.Action),
                    new TileBinding(shortcut, origin, injection),
                    shortcut.Icon,
                    shortcut.Category
                )
            );
        }

        return tiles.MoveToImmutable();
    }
}
