namespace Clicalo.UI.Wpf.Surfaces.TabView;

/// <summary>Where the icon of a <see cref="DockTool"/> goes.</summary>
internal enum DockToolShape
{
    /// <summary>Before the label, in a row of 44 (a side bar).</summary>
    Row,

    /// <summary>Above the label, in a card of 52 (the segments of «Qué ver»).</summary>
    Stacked,

    /// <summary>
    /// The icon alone, a square of 44 like the icon buttons beside it, so two go side by side across a side bar (D19);
    /// the label is the accessible name.
    /// </summary>
    Icon,
}
