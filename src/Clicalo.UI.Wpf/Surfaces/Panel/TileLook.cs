using Clicalo.Domain.Catalog;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces.Panel;

/// <summary>
/// The anatomy of a tile of the panel in direction C (D12, CUA-007, CUA-011): the box of its icon, the style of its
/// name and of its key line (both grow with the text scale) and the lines its name may take before «…». S takes the
/// small icon and the small label, so a name of the narrowest tile is still read whole at the largest text (CUA-011),
/// and its tile has no room for the key line; M takes the label of 14 and L the <c>heading</c> name and the medium
/// icon, so the size the person chose is seen in the text too. Each fits whole in the height of its size (D20): icon,
/// one line of name and the key line, or two lines of name.
/// </summary>
/// <param name="IconSize">The icon box (<see cref="IconBox"/>).</param>
/// <param name="Name">The style of the name.</param>
/// <param name="Keys">The style of the key line.</param>
/// <param name="NameLines">Lines of the name before «…»: 1 or 2.</param>
/// <param name="TopInset">The space above the body: under the slot of the voice number when the tile is too narrow.</param>
/// <param name="ShowsIcon">Whether the icon shows; false only where it has no room (see <see cref="Strip"/>).</param>
/// <param name="SideInset">The space at each side of the body.</param>
/// <param name="LeadInset">
/// The space before the body when it differs from <paramref name="SideInset"/>: the slot of the voice number beside it.
/// </param>
/// <param name="CentresNumber">Whether the voice number is centred down the tile, beside the body, not at its top.</param>
internal sealed record TileLook(
    double IconSize,
    TextStyleToken Name,
    TextStyleToken Keys,
    int NameLines,
    double TopInset = Space.S1,
    bool ShowsIcon = true,
    double SideInset = Space.S2,
    double? LeadInset = null,
    bool CentresNumber = false
)
{
    /// <summary>The tiles of the grid in <paramref name="size"/>.</summary>
    /// <param name="size">S, M or L.</param>
    public static TileLook Grid(PanelSize size) =>
        size switch
        {
            PanelSize.S => new(
                IconBox.Small,
                TextStyleToken.LabelSmall,
                TextStyleToken.KeysTile,
                2
            ),
            PanelSize.L => new(IconBox.Medium, TextStyleToken.Heading, TextStyleToken.Keys, 2),
            _ => new(IconBox.Small, TextStyleToken.Label, TextStyleToken.KeysTile, 2),
        };

    /// <summary>
    /// The tiles of the Always visible row, in Full and Compact: the small label in one line with «…» and the narrow side
    /// inset of a narrow tile, in the 56 of M and L (D20). With the voice numbers on in four narrow columns
    /// (<paramref name="cramped"/>, ACC-009) the icon is the small one, so it stays beside the slot of the number and
    /// never under it; a larger text grows the name, which still fits under the icon (CUA-011).
    /// </summary>
    /// <param name="cramped">Whether the voice number needs the top of the tile: numbers on in four narrow columns.</param>
    /// <param name="showsNames">
    /// Whether the row shows names. A row of icons alone (S, Compact: one touch target high, D19) takes the small icon,
    /// centred and clear of the type mark at the top right; with the voice numbers on, the number and the icon go side by
    /// side, both centred down the 44 of the tile (a number above would push the icon against the bottom edge).
    /// </param>
    public static TileLook Strip(bool cramped, bool showsNames = true) =>
        !showsNames
            ? new(
                IconBox.Small,
                TextStyleToken.LabelSmall,
                TextStyleToken.KeysTile,
                1,
                SideInset: Space.S1,
                LeadInset: cramped ? Space.S6 : null,
                CentresNumber: cramped
            )
            : new(
                cramped ? IconBox.Small : IconBox.Medium,
                TextStyleToken.LabelSmall,
                TextStyleToken.KeysTile,
                1,
                SideInset: Space.S1
            );
}
