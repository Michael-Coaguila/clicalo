using Clicalo.Domain.Catalog;
using Clicalo.UI.Wpf.Resources;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces.Panel;

/// <summary>
/// The anatomy of a tile of the panel in direction C (D12, CUA-007, CUA-011): the box of its icon, the style of its
/// name and of its key line (both grow with the text scale) and the lines its name may take before «…». S takes the
/// small icon and the small label, so a name of the narrowest tile is still read whole at the largest text (CUA-011),
/// and its tile has no room for the key line (a result or a Frequents tile gives up its icon for its profile of origin,
/// <see cref="Origin"/>); M takes the label of 14 and L the <c>heading</c> name and the medium
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
/// <param name="CentresNumber">
/// Whether the voice number is centred down the tile, beside the body, not at its top: the body then starts
/// <see cref="Space.S1"/> after the number whatever its digits.
/// </param>
/// <param name="KeysAreWords">
/// Whether the line under the name holds words (the profile of origin), drawn in <c>labelSmall</c> in the interface
/// font and not in the monospace of the keys, which the design system keeps for keys. It takes the whole inner width
/// of the tile, <see cref="Space.S1"/> inside its edge as the slots of its corners, and ends in «…» only when it does
/// not fit there.
/// </param>
/// <param name="ClearsCornerNumber">
/// Whether the body has no icon beside the voice number, which stays in its corner at the top left as on every tile:
/// the name and its origin go under the number when they fit there, otherwise the name goes beside it, in the room it
/// leaves, and the origin keeps the whole width under them (<see cref="Origin"/>).
/// </param>
internal sealed record TileLook(
    double IconSize,
    TextStyleToken Name,
    TextStyleToken Keys,
    int NameLines,
    double TopInset = Space.S1,
    bool ShowsIcon = true,
    double SideInset = Space.S2,
    double? LeadInset = null,
    bool CentresNumber = false,
    bool KeysAreWords = false,
    bool ClearsCornerNumber = false
)
{
    /// <summary>
    /// A Frequents tile or a search result (FRE-003, BUS-005): the profile of origin goes under its name, in one line of
    /// words (<see cref="KeysAreWords"/>), and the name takes one line with «…», as a tile with its key line does (D20).
    /// Where the tile has room for its icon, one line of name and the origin (Full in M, L) it keeps the anatomy of the
    /// grid. Where it has not (Compact; S, whose name takes the place of the key line; M at a larger text) the origin is
    /// the line that tells two results of one name apart, so it takes the place of the icon: the name and the origin are
    /// centred down the tile, with the narrow side inset so the name has the width of the tile, and with the voice
    /// numbers on (<paramref name="numbered"/>, ACC-009) the number stays in its corner, where every tile has it, and the
    /// two keep clear of it (<see cref="ClearsCornerNumber"/>).
    /// </summary>
    /// <param name="grid">The look of the grid in the size of the panel (<see cref="Grid"/>).</param>
    /// <param name="height">The height of the tile.</param>
    /// <param name="textScalePercent">The person's text scale (CUA-011).</param>
    /// <param name="numbered">Whether the voice numbers are on.</param>
    public static TileLook Origin(TileLook grid, double height, int textScalePercent, bool numbered)
    {
        ArgumentNullException.ThrowIfNull(grid);
        var words = grid with
        {
            Keys = TextStyleToken.LabelSmall,
            NameLines = 1,
            KeysAreWords = true,
        };
        var lines =
            LineOf(grid.Name, textScalePercent)
            + Space.S1
            + LineOf(TextStyleToken.LabelSmall, textScalePercent);
        // To the nearest pixel, as the body of a tile lays out its lines.
        var withIcon = grid.TopInset + grid.IconSize + Space.S1 + lines + Space.S1;
        return Math.Round(withIcon, MidpointRounding.AwayFromZero) <= height
            ? words
            : words with
            {
                ShowsIcon = false,
                SideInset = Space.S1,
                ClearsCornerNumber = numbered,
            };
    }

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

    /// <summary>The height of one line of <paramref name="style"/> at the text scale, as a tile lays it out.</summary>
    private static double LineOf(TextStyleToken style, int textScalePercent)
    {
        var spec = TextStyles.Get(style);
        var family = spec.Family == TextFamily.Keys ? AppFonts.Mono : AppFonts.Ui;
        return family.LineSpacing * TypeScale.Scale(spec.Size, textScalePercent);
    }
}
