namespace Clicalo.UI.Wpf.Controls;

/// <summary>
/// The shapes of an option that can be chosen (direction C, D12). Every kind shows «elegido» the same way: wash, a
/// ring of 2 inside and a filled icon, or a ✓ when the option has no icon; the option never changes size.
/// </summary>
public enum OptionKind
{
    /// <summary>A card of 44 with its line: an icon or ✓ and the text in a row (S · M · L, Ctrl, letters, Tema).</summary>
    Tile,

    /// <summary>A card of 64 with its line: the icon above the text (Vista, the kinds of the editor, presets).</summary>
    Card,

    /// <summary>A pill of 44 with its line: ✓ or icon and the text (groups of keys, apps of the welcome).</summary>
    Chip,

    /// <summary>A segment of 44 without fill, inside the track of a <see cref="SegmentedControl"/> (Frecuentes | Word, ES | EN).</summary>
    Segment,

    /// <summary>A row of 52 without fill: icon, title and caption, left aligned (the list of profiles).</summary>
    ListRow,

    /// <summary>An item of the side rail without fill: the icon, a counter beside it and a label of up to two lines.</summary>
    Rail,
}
