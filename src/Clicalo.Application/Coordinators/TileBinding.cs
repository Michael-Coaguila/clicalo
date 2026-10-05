using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;

namespace Clicalo.Application.Coordinators;

/// <summary>
/// What a tile of the panel runs (blueprint §7.1): its shortcut and the profile it is shown from, whose injection mode
/// the activation carries (D24: General inherits the mode of the profile resolved for the foreground app; in M2 the
/// panel shows one profile and passes its own mode).
/// </summary>
/// <param name="Shortcut">The shortcut.</param>
/// <param name="OriginProfile">The profile the tile is shown from.</param>
/// <param name="Injection">How its keys are sent (EJE-003, ATJ-004).</param>
public sealed record TileBinding(
    Shortcut Shortcut,
    ProfileId? OriginProfile,
    InjectionMode Injection
);
