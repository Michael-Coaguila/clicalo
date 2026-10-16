using System.Globalization;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Documents;
using System.Windows.Media;
using Clicalo.UI.Wpf.Resources;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Controls;

/// <summary>
/// The only section label of direction C (D12): the <see cref="TextStyleToken.Overline"/> style (bold 12/16, in
/// capitals with a letter spacing of 0.06 em) in <c>muted</c>. One line; when it has no room it ends in «…». WPF has
/// no letter spacing, so the label lays its glyphs out itself.
/// </summary>
/// <remarks>
/// UI Automation sees a Text element named with <see cref="Text"/> as written (not in capitals), so a screen reader
/// reads the words, not the letters (REG-06). Product text only, already localized (CLC0006).
/// </remarks>
public sealed class Overline : FrameworkElement
{
    /// <summary>Identifies <see cref="Text"/>.</summary>
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(Overline),
        new FrameworkPropertyMetadata(
            string.Empty,
            FrameworkPropertyMetadataOptions.AffectsMeasure
                | FrameworkPropertyMetadataOptions.AffectsRender
        )
    );

    /// <summary>Identifies <see cref="Foreground"/>.</summary>
    public static readonly DependencyProperty ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner(
            typeof(Overline),
            new FrameworkPropertyMetadata(
                SystemColors.ControlTextBrush,
                FrameworkPropertyMetadataOptions.Inherits
                    | FrameworkPropertyMetadataOptions.AffectsRender
            )
        );

    private const string Ellipsis = "…";

    [ThreadStatic]
    private static GlyphTypeface? _face;

    private ushort[] _glyphs = [];
    private double[] _advances = [];

    /// <summary>Creates a label in <c>muted</c>.</summary>
    public Overline()
    {
        SetResourceReference(ForegroundProperty, ThemeBrushKey.For(ColorToken.Muted));
        SnapsToDevicePixels = true;
    }

    /// <summary>Creates the label <paramref name="text"/>.</summary>
    public Overline(string text)
        : this() => Text = text;

    /// <summary>The label as written; it is drawn in capitals.</summary>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>The color of the label (<c>muted</c> by default).</summary>
    public Brush? Foreground
    {
        get => (Brush?)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>The label as drawn: capitals, and «…» when it has no room.</summary>
    public string Shown { get; private set; } = string.Empty;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var spec = TextStyles.Overline;
        var face = Face();
        var upper = (Text ?? string.Empty).ToUpper(CultureInfo.CurrentCulture);
        if (face is null || upper.Length == 0)
        {
            _glyphs = [];
            _advances = [];
            Shown = upper;
            return new Size(0, upper.Length == 0 ? 0 : spec.LineHeight);
        }

        var tracking = spec.LetterSpacing * spec.Size;
        var text = upper;
        while (true)
        {
            Lay(face, text, spec.Size, tracking);
            var width = LaidWidth(tracking);
            if (width <= availableSize.Width || text.Length <= 1)
            {
                Shown = text;
                return new Size(Math.Min(width, availableSize.Width), spec.LineHeight);
            }

            // Drop letters until the label and «…» fit.
            var trimmed = (text.EndsWith(Ellipsis, StringComparison.Ordinal) ? text[..^1] : text)
                .TrimEnd();
            text = trimmed[..^1].TrimEnd() + Ellipsis;
        }
    }

    /// <inheritdoc />
    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);
        var face = Face();
        if (face is null || _glyphs.Length == 0 || Foreground is not { } brush)
        {
            return;
        }

        var spec = TextStyles.Overline;
        var ascent = face.Baseline * spec.Size;
        var height = face.Height * spec.Size;
        var baseline = ((spec.LineHeight - height) / 2) + ascent;
        var run = new GlyphRun(
            face,
            bidiLevel: 0,
            isSideways: false,
            renderingEmSize: spec.Size,
            pixelsPerDip: (float)VisualTreeHelper.GetDpi(this).PixelsPerDip,
            glyphIndices: _glyphs,
            baselineOrigin: new Point(0, baseline),
            advanceWidths: _advances,
            glyphOffsets: null,
            characters: null,
            deviceFontName: null,
            clusterMap: null,
            caretStops: null,
            language: null
        );
        drawingContext.PushClip(new RectangleGeometry(new Rect(RenderSize)));
        drawingContext.DrawGlyphRun(brush, run);
        drawingContext.Pop();
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new OverlineAutomationPeer(this);

    private void Lay(GlyphTypeface face, string text, double size, double tracking)
    {
        var glyphs = new List<ushort>(text.Length);
        var advances = new List<double>(text.Length);
        foreach (var c in text)
        {
            if (!face.CharacterToGlyphMap.TryGetValue(c, out var glyph))
            {
                glyph = face.CharacterToGlyphMap.TryGetValue('?', out var fallback) ? fallback : (ushort)0;
            }

            glyphs.Add(glyph);
            advances.Add((face.AdvanceWidths[glyph] * size) + tracking);
        }

        _glyphs = [.. glyphs];
        _advances = [.. advances];
    }

    private double LaidWidth(double tracking) =>
        _advances.Length == 0 ? 0 : _advances.Sum() - tracking;

    private static GlyphTypeface? Face() =>
        _face ??= new Typeface(
            AppFonts.Ui,
            FontStyles.Normal,
            FontWeights.Bold,
            FontStretches.Normal
        ).TryGetGlyphTypeface(out var face)
            ? face
            : null;
}
