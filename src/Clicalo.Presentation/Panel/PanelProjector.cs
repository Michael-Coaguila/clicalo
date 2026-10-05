using System.Collections.Immutable;
using Clicalo.Application.Coordinators;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.Panel;

/// <summary>
/// The pure projection of the M2 panel (blueprint §8.2): the shortcuts of one profile become tiles, in display order,
/// with their name in the interface language and the injection mode of their profile (D24). View models only apply
/// its result; they decide no product rule. The full <c>PanelProjector</c> of M3 (layers, pages, Frequents, dimming)
/// replaces it in <c>Application.Projections</c>.
/// </summary>
public static class PanelProjector
{
    /// <summary>Projects <paramref name="profile"/>.</summary>
    /// <param name="profile">The profile in view.</param>
    /// <param name="language">The interface language.</param>
    /// <param name="fallback">The language used when a name lacks <paramref name="language"/> (the default one).</param>
    public static PanelModel Project(Profile profile, LangCode language, LangCode fallback)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var tiles = ImmutableArray.CreateBuilder<TileModel>(profile.Shortcuts.Count);
        foreach (var shortcut in profile.Shortcuts)
        {
            tiles.Add(
                new TileModel(
                    shortcut.Id,
                    shortcut.Name.Get(language, fallback),
                    BehaviorOf(shortcut.Action),
                    new TileBinding(shortcut, profile.Id, profile.Injection)
                )
            );
        }

        return new PanelModel(profile.Id, tiles.MoveToImmutable());
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
}
