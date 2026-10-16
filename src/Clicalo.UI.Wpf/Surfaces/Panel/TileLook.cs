using Clicalo.Domain.Catalog;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces.Panel;

/// <summary>
/// The anatomy of a tile of the panel in direction C (D12, CUA-007, CUA-011): the box of its icon, the style of its
/// name and of its key line (both grow with the text scale) and the lines its name may take before «…». S takes the
/// small icon and the small label, so a name of the narrowest tile is still read whole at the largest text (CUA-011); L
/// takes the larger icon and the <c>heading</c> name, so the size the person chose is seen in the text too.
/// </summary>
/// <param name="IconSize">The icon box (<see cref="IconBox"/>).</param>
/// <param name="Name">The style of the name.</param>
/// <param name="Keys">The style of the key line.</param>
/// <param name="NameLines">Lines of the name before «…»: 1 or 2.</param>
/// <param name="TopInset">The space above the body: under the slot of the voice number when the tile is too narrow.</param>
/// <param name="ShowsIcon">Whether the icon shows; false only where it has no room (see <see cref="Strip"/>).</param>
internal sealed record TileLook(
    double IconSize,
    TextStyleToken Name,
    TextStyleToken Keys,
    int NameLines,
    double TopInset = Space.S1,
    bool ShowsIcon = true
)
{
    /// <summary>The tiles of the grid in <paramref name="size"/>.</summary>
    /// <param name="size">S, M or L.</param>
    public static TileLook Grid(PanelSize size) =>
        size switch
        {
            PanelSize.S => new(IconBox.Small, TextStyleToken.LabelSmall, TextStyleToken.KeysTile, 2),
            PanelSize.L => new(IconBox.Large, TextStyleToken.Heading, TextStyleToken.Keys, 2),
            _ => new(IconBox.Medium, TextStyleToken.Label, TextStyleToken.KeysTile, 2),
        };

    /// <summary>
    /// The tiles of the Always visible row, in Full and Compact (64 high): the icon of the grid and the small label. Its
    /// four columns are too narrow to keep the voice number of the top left slot beside the icon, so with the numbers on
    /// (ACC-009) the body starts under that slot, with the small icon and one line of name; at a larger text the icon
    /// gives way, so the number never covers it and the name is never cut (CUA-011).
    /// </summary>
    /// <param name="voiceNumbers">Whether the voice numbers show.</param>
    /// <param name="largeText">Whether the text is above 100 %.</param>
    public static TileLook Strip(bool voiceNumbers, bool largeText) =>
        voiceNumbers
            ? new(
                IconBox.Small,
                TextStyleToken.LabelSmall,
                TextStyleToken.KeysTile,
                1,
                Space.S6,
                ShowsIcon: !largeText
            )
            : new(IconBox.Medium, TextStyleToken.LabelSmall, TextStyleToken.KeysTile, 2);
}
