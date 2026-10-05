using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Clicalo.UI.Wpf.Resources;

namespace Clicalo.UI.Wpf.Controls;

/// <summary>
/// A Material Symbols Rounded icon (TEM-005, docs/07 «Tipografía»): the glyph of <see cref="Symbol"/>, drawn by its
/// code point from the bundled font in a <see cref="Size"/> × <see cref="Size"/> box, in the inherited
/// <see cref="Foreground"/>; filled (FILL 1) when <see cref="IsFilled"/>. An unknown name draws the fallback icon
/// (<see cref="MaterialSymbols.FallbackName"/>).
/// </summary>
/// <remarks>
/// The icon is decorative: it is not a UI Automation element and never carries a name, so no glyph name reaches a
/// screen reader (UIA008). The control that contains it says what it does. Like CSS <c>line-height: 1</c> in the
/// prototype, the box is exactly the icon size, and the glyph is placed from the font's 960-unit grid, not from the
/// font's line metrics.
/// </remarks>
public sealed class SymbolIcon : FrameworkElement
{
    /// <summary>Identifies <see cref="Symbol"/>.</summary>
    public static readonly DependencyProperty SymbolProperty = DependencyProperty.Register(
        nameof(Symbol),
        typeof(string),
        typeof(SymbolIcon),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender)
    );

    /// <summary>Identifies <see cref="Size"/>.</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size),
        typeof(double),
        typeof(SymbolIcon),
        new FrameworkPropertyMetadata(
            20d,
            FrameworkPropertyMetadataOptions.AffectsMeasure
                | FrameworkPropertyMetadataOptions.AffectsRender
        ),
        static value => value is double size && size >= 0 && double.IsFinite(size)
    );

    /// <summary>Identifies <see cref="IsFilled"/>.</summary>
    public static readonly DependencyProperty IsFilledProperty = DependencyProperty.Register(
        nameof(IsFilled),
        typeof(bool),
        typeof(SymbolIcon),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender)
    );

    /// <summary>Identifies <see cref="Foreground"/> (the inherited text color).</summary>
    public static readonly DependencyProperty ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner(
            typeof(SymbolIcon),
            new FrameworkPropertyMetadata(
                SystemColors.ControlTextBrush,
                FrameworkPropertyMetadataOptions.Inherits
                    | FrameworkPropertyMetadataOptions.AffectsRender
            )
        );

    [ThreadStatic]
    private static GlyphTypeface? _outlined;

    [ThreadStatic]
    private static GlyphTypeface? _filled;

    /// <summary>The Material Symbols name of the icon, such as <c>bolt</c>.</summary>
    public string? Symbol
    {
        get => (string?)GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }

    /// <summary>Side of the icon, in device-independent pixels (20 by default).</summary>
    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <summary>True to draw the filled variant (FILL 1), for active states.</summary>
    public bool IsFilled
    {
        get => (bool)GetValue(IsFilledProperty);
        set => SetValue(IsFilledProperty, value);
    }

    /// <summary>The color of the icon; inherited like the text color.</summary>
    public Brush? Foreground
    {
        get => (Brush?)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>The glyph index of <paramref name="symbol"/> in the bundled font, or null when the font is missing.</summary>
    /// <param name="symbol">A Material Symbols name; an unknown name gives the fallback icon.</param>
    /// <param name="filled">True for the filled variant.</param>
    public static ushort? GlyphIndexOf(string? symbol, bool filled)
    {
        var typeface = Typeface(filled);
        return
            typeface is not null
            && typeface.CharacterToGlyphMap.TryGetValue(
                MaterialSymbols.CodePointOrFallback(symbol),
                out var index
            )
            ? index
            : null;
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    /// <inheritdoc />
    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);
        var size = Size;
        var typeface = Typeface(IsFilled);
        if (
            size <= 0
            || Foreground is not { } brush
            || typeface is null
            || !typeface.CharacterToGlyphMap.TryGetValue(
                MaterialSymbols.CodePointOrFallback(Symbol),
                out var glyph
            )
        )
        {
            return;
        }

        // The icons are drawn on a square grid that sits on the baseline: the baseline is the bottom of the box.
        var left = (RenderSize.Width - size) / 2;
        var top = (RenderSize.Height - size) / 2;
        var run = new GlyphRun(
            typeface,
            bidiLevel: 0,
            isSideways: false,
            renderingEmSize: size,
            pixelsPerDip: (float)VisualTreeHelper.GetDpi(this).PixelsPerDip,
            glyphIndices: [glyph],
            baselineOrigin: new Point(left, top + size),
            advanceWidths: [typeface.AdvanceWidths[glyph] * size],
            glyphOffsets: null,
            characters: null,
            deviceFontName: null,
            clusterMap: null,
            caretStops: null,
            language: null
        );
        drawingContext.DrawGlyphRun(brush, run);
    }

    private static GlyphTypeface? Typeface(bool filled)
    {
        if (filled)
        {
            return _filled ??= Load(AppFonts.SymbolsFilled);
        }

        return _outlined ??= Load(AppFonts.Symbols);
    }

    private static GlyphTypeface? Load(FontFamily family) =>
        new Typeface(
            family,
            FontStyles.Normal,
            FontWeights.Normal,
            FontStretches.Normal
        ).TryGetGlyphTypeface(out var glyphTypeface)
            ? glyphTypeface
            : null;
}
