using System.Collections.Frozen;
using System.Windows;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Theming;

/// <summary>
/// Resource keys of the theme resources that are not colors (TEM-005, TEM-006, CUA-011): the bundled font families,
/// the sizes of the type scale (design and scaled by the person's text scale) and reduce motion. Colors use
/// <see cref="Controls.ThemeBrushKey"/> and <see cref="Controls.CategoryBrushKey"/>; borders use
/// <see cref="Controls.ThemeScope.BorderThicknessKey"/>.
/// </summary>
public static class ThemeKeys
{
    private static readonly FrozenDictionary<double, ResourceKey> DesignSizes =
        TypeScale.Steps.ToFrozenDictionary(px => px, px => (ResourceKey)Key("TextSize" + px));

    private static readonly FrozenDictionary<double, ResourceKey> ScaledSizes =
        TypeScale.Steps.ToFrozenDictionary(px => px, px => (ResourceKey)Key("ScaledTextSize" + px));

    private static readonly FrozenDictionary<TextStyleToken, ResourceKey> StyleSizes =
        TextStyles.All.ToFrozenDictionary(s => s, s => (ResourceKey)Key("FontSize." + s));

    private static readonly FrozenDictionary<TextStyleToken, ResourceKey> StyleLines =
        TextStyles.All.ToFrozenDictionary(s => s, s => (ResourceKey)Key("LineHeight." + s));

    private static readonly FrozenDictionary<TextStyleToken, ResourceKey> ScaledStyleSizes =
        TextStyles.All.ToFrozenDictionary(s => s, s => (ResourceKey)Key("ScaledFontSize." + s));

    private static readonly FrozenDictionary<TextStyleToken, ResourceKey> ScaledStyleLines =
        TextStyles.All.ToFrozenDictionary(s => s, s => (ResourceKey)Key("ScaledLineHeight." + s));

    /// <summary>
    /// A <see cref="double"/>: the thickness of the rest line of controls and cards (<see cref="Stroke.Line"/>; 2 in
    /// high contrast, as the palette's border). Always drawn inside the control.
    /// </summary>
    public static ResourceKey LineThickness { get; } = Key(nameof(LineThickness));

    /// <summary>
    /// A <see cref="double"/>: the opacity of the content of a disabled control. 1 where <c>muted</c> already tells it
    /// apart; less in high contrast, where <c>muted</c> is the text color and a disabled ‹ looked like an enabled one.
    /// </summary>
    public static ResourceKey DisabledOpacity { get; } = Key(nameof(DisabledOpacity));

    /// <summary>The <see cref="System.Windows.Media.FontFamily"/> of the interface (Atkinson Hyperlegible).</summary>
    public static ResourceKey UiFont { get; } = Key(nameof(UiFont));

    /// <summary>The <see cref="System.Windows.Media.FontFamily"/> of keys and processes (JetBrains Mono).</summary>
    public static ResourceKey MonoFont { get; } = Key(nameof(MonoFont));

    /// <summary>The <see cref="System.Windows.Media.FontFamily"/> of outlined icons (Material Symbols, FILL 0).</summary>
    public static ResourceKey SymbolsFont { get; } = Key(nameof(SymbolsFont));

    /// <summary>The <see cref="System.Windows.Media.FontFamily"/> of filled icons (Material Symbols, FILL 1).</summary>
    public static ResourceKey SymbolsFilledFont { get; } = Key(nameof(SymbolsFilledFont));

    /// <summary>A <see cref="bool"/>: true when transitions must take 0 ms (TEM-006).</summary>
    public static ResourceKey ReduceMotion { get; } = Key(nameof(ReduceMotion));

    /// <summary>An <see cref="int"/>: the person's text scale in percent (CUA-011).</summary>
    public static ResourceKey TextScalePercent { get; } = Key(nameof(TextScalePercent));

    /// <summary>The <see cref="double"/> size of one step of <see cref="TypeScale.Steps"/>, as designed.</summary>
    /// <param name="designPx">A step of the scale.</param>
    public static ResourceKey TextSize(double designPx) => Lookup(DesignSizes, designPx);

    /// <summary>
    /// The <see cref="double"/> size of one step of <see cref="TypeScale.Steps"/> scaled by the person's text
    /// scale (<see cref="TypeScale.Scale"/>): for tile names, key lines and the fixed row (CUA-011).
    /// </summary>
    /// <param name="designPx">A step of the scale.</param>
    public static ResourceKey ScaledTextSize(double designPx) => Lookup(ScaledSizes, designPx);

    /// <summary>
    /// The <see cref="double"/> font size of a text style of direction C (<see cref="TextStyles"/>); scaled by the
    /// person's text scale when <paramref name="scaled"/> (tile names, key lines and the fixed row, CUA-011).
    /// </summary>
    public static ResourceKey FontSize(TextStyleToken style, bool scaled = false) =>
        LookupStyle(scaled ? ScaledStyleSizes : StyleSizes, style);

    /// <summary>The <see cref="double"/> line height of a text style; scaled with its font size when <paramref name="scaled"/>.</summary>
    public static ResourceKey LineHeight(TextStyleToken style, bool scaled = false) =>
        LookupStyle(scaled ? ScaledStyleLines : StyleLines, style);

    /// <summary>The font family key of a text style: <see cref="MonoFont"/> for keys, <see cref="UiFont"/> otherwise.</summary>
    public static ResourceKey FontFamily(TextStyleToken style) =>
        TextStyles.Get(style).Family == TextFamily.Keys ? MonoFont : UiFont;

    private static ComponentResourceKey Key(string name) => new(typeof(ThemeKeys), name);

    private static ResourceKey LookupStyle(
        FrozenDictionary<TextStyleToken, ResourceKey> keys,
        TextStyleToken style
    ) =>
        keys.TryGetValue(style, out var key)
            ? key
            : throw new ArgumentOutOfRangeException(nameof(style), style, message: null);

    private static ResourceKey Lookup(
        FrozenDictionary<double, ResourceKey> keys,
        double designPx
    ) =>
        keys.TryGetValue(designPx, out var key)
            ? key
            : throw new ArgumentOutOfRangeException(
                nameof(designPx),
                designPx,
                "Not a step of the type scale."
            );
}
