using System.Collections.Immutable;
using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.Panel;

/// <summary>
/// What the M2 panel shows (the <c>PanelModel</c> of blueprint §8.2 in its walking-skeleton form): the tiles of the
/// profile in view, in display order. Pages, the fixed row, Frequents, voice numbers and dimming join in M3.
/// </summary>
/// <param name="Profile">The profile in view.</param>
/// <param name="Tiles">Its tiles in display order.</param>
public sealed record PanelModel(ProfileId Profile, ImmutableArray<TileModel> Tiles)
{
    /// <summary>A panel with no tiles (before the document is loaded).</summary>
    public static PanelModel Empty { get; } = new(ProfileId.General, []);
}
