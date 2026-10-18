using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.PanelLayout;
using Clicalo.UI.Wpf.Resources;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces.Panel;

/// <summary>
/// The name of a tile of the panel against the room it has (CUA-011, D17): the width the name gets in a tile of the
/// panel laid in a number of columns, and the name laid out in that width with the style of its tile at the person's
/// text scale. A word is never cut: the widest word of a name (<see cref="FormattedText.MinWidth"/>) must fit the room,
/// or the grid or the row gives up columns. The fonts are the app's own, so it measures the same before the view is on
/// screen.
/// </summary>
internal static class TileNameFit
{
    /// <summary>The width the name of a tile has when the panel lays its tiles in <paramref name="columns"/>.</summary>
    /// <param name="layout">The layout of the panel (its width, PAN-002).</param>
    /// <param name="columns">The columns of the grid or the row.</param>
    /// <param name="look">The anatomy of the tile.</param>
    public static double Room(PanelLayoutSettings layout, int columns, TileLook look)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(look);

        // Each tile keeps half the gap around it (Space.S1) and the grid reaches as far past the body.
        var content = GridMetrics.PanelWidth(layout) - PanelSizes.Layout.PanelChromeWidthPx;
        var tile = ((content + (2 * Space.S1)) / columns) - (2 * Space.S1);
        return tile - (2 * look.SideInset) - (2 * Stroke.Line);
    }

    /// <summary><paramref name="name"/> laid out in <paramref name="room"/> with the name style of <paramref name="look"/>.</summary>
    /// <param name="name">The name of the tile.</param>
    /// <param name="look">The anatomy of the tile.</param>
    /// <param name="textScalePercent">The person's text scale (CUA-011).</param>
    /// <param name="room">The width the name has.</param>
    /// <param name="view">The view that measures: its text color and the scale of its screen.</param>
    public static FormattedText Layout(
        string name,
        TileLook look,
        int textScalePercent,
        double room,
        FrameworkElement view
    )
    {
        ArgumentNullException.ThrowIfNull(look);
        ArgumentNullException.ThrowIfNull(view);
        var spec = TextStyles.Get(look.Name);
        return new FormattedText(
            name,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(
                Family(look),
                FontStyles.Normal,
                FontWeight.FromOpenTypeWeight(spec.Weight),
                FontStretches.Normal
            ),
            TypeScale.Scale(spec.Size, textScalePercent),
            TextElement.GetForeground(view),
            VisualTreeHelper.GetDpi(view).PixelsPerDip
        )
        {
            MaxTextWidth = Math.Max(1, room),
        };
    }

    /// <summary>Whether every word of <paramref name="name"/> fits <paramref name="room"/>: none would end in «…».</summary>
    /// <param name="name">The name of the tile.</param>
    /// <param name="look">The anatomy of the tile.</param>
    /// <param name="textScalePercent">The person's text scale.</param>
    /// <param name="room">The width the name has.</param>
    /// <param name="view">The view that measures.</param>
    public static bool WordsFit(
        string name,
        TileLook look,
        int textScalePercent,
        double room,
        FrameworkElement view
    ) => Layout(name, look, textScalePercent, room, view).MinWidth <= room;

    /// <summary>The lines <paramref name="text"/> takes, laid out by <see cref="Layout"/>.</summary>
    /// <param name="text">The name laid out.</param>
    /// <param name="look">The anatomy of the tile.</param>
    /// <param name="textScalePercent">The text scale it was laid out with.</param>
    public static int Lines(FormattedText text, TileLook look, int textScalePercent)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(look);
        var size = TypeScale.Scale(TextStyles.Get(look.Name).Size, textScalePercent);
        return (int)Math.Round(text.Height / (size * Family(look).LineSpacing));
    }

    private static FontFamily Family(TileLook look) =>
        TextStyles.Get(look.Name).Family == TextFamily.Keys ? AppFonts.Mono : AppFonts.Ui;
}
