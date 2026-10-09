using System.Collections.Immutable;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.Panel;

/// <summary>
/// What the panel shows from the document (the <c>PanelModel</c> of blueprint §8.2): the tiles of the list in view in
/// display order, the Always visible row, and the name and icon of the profile in view and of every profile for the
/// selector and the profile grid. Pages, rows and voice numbers are decided by <c>Clicalo.Domain.PanelLayout</c> when
/// the view model applies it.
/// </summary>
/// <param name="Profile">The profile in view (or the return profile, while Frequents is in view).</param>
/// <param name="Tiles">The tiles of the list in view, in display order.</param>
/// <param name="Strip">The Always visible row, in display order (FIJ-001); default when the projection has none.</param>
/// <param name="ProfileName">The name of <paramref name="Profile"/> in the interface language.</param>
/// <param name="ProfileIcon">The icon of <paramref name="Profile"/>.</param>
/// <param name="Profiles">Every profile, in the stored order, for the profile grid (SEL-003); default when none.</param>
public sealed record PanelModel(
    ProfileId Profile,
    ImmutableArray<TileModel> Tiles,
    ImmutableArray<TileModel> Strip = default,
    string ProfileName = "",
    IconRef? ProfileIcon = null,
    ImmutableArray<PickerEntry> Profiles = default
)
{
    /// <summary>A panel with no tiles (before the document is loaded).</summary>
    public static PanelModel Empty { get; } = new(ProfileId.General, []);

    /// <summary>The Always visible row; empty when the projection has none.</summary>
    public ImmutableArray<TileModel> StripTiles => Strip.IsDefault ? [] : Strip;

    /// <summary>Every profile; empty when the projection has none.</summary>
    public ImmutableArray<PickerEntry> PickerEntries => Profiles.IsDefault ? [] : Profiles;
}
