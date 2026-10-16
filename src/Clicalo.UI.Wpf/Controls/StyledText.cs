using System.Windows;
using System.Windows.Controls;
using Clicalo.UI.Wpf.Resources;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Controls;

/// <summary>
/// A text of the closed type scale of direction C (D12, <see cref="TextStyles"/>), with the overflow rule of its
/// place: one line ending in «…» by default (<see cref="MaxLines"/> = 1), two lines and «…» where a design says so,
/// or wrapping without limit (<see cref="MaxLines"/> = 0, paragraphs). A line never breaks inside a word, so an
/// identifier such as WINWORD.EXE is never split. Font, size, line height and color are theme resources: a theme or
/// text scale change repaints it in place.
/// </summary>
/// <remarks>
/// <see cref="IsScaled"/> follows the person's text scale (CUA-011: tile names, key lines and the fixed row); every
/// other text keeps its design size. Product text only, already localized (CLC0006).
/// </remarks>
public sealed class StyledText : TextBlock
{
    /// <summary>Identifies <see cref="TextStyle"/>.</summary>
    public static readonly DependencyProperty TextStyleProperty = DependencyProperty.Register(
        nameof(TextStyle),
        typeof(TextStyleToken),
        typeof(StyledText),
        new FrameworkPropertyMetadata(TextStyleToken.Body, OnLookChanged)
    );

    /// <summary>Identifies <see cref="Ink"/>.</summary>
    public static readonly DependencyProperty InkProperty = DependencyProperty.Register(
        nameof(Ink),
        typeof(ColorToken),
        typeof(StyledText),
        new FrameworkPropertyMetadata(ColorToken.Text, OnInkChanged)
    );

    /// <summary>Identifies <see cref="MaxLines"/>.</summary>
    public static readonly DependencyProperty MaxLinesProperty = DependencyProperty.Register(
        nameof(MaxLines),
        typeof(int),
        typeof(StyledText),
        new FrameworkPropertyMetadata(1, OnMaxLinesChanged),
        static value => value is int lines && lines >= 0
    );

    /// <summary>Identifies <see cref="IsScaled"/>.</summary>
    public static readonly DependencyProperty IsScaledProperty = DependencyProperty.Register(
        nameof(IsScaled),
        typeof(bool),
        typeof(StyledText),
        new FrameworkPropertyMetadata(false, OnLookChanged)
    );

    static StyledText() =>
        LineHeightProperty.OverrideMetadata(
            typeof(StyledText),
            new FrameworkPropertyMetadata(OnLinesChanged)
        );

    /// <summary>Creates a one-line body text in <c>text</c>.</summary>
    public StyledText()
    {
        Apply(this, TextStyleToken.Body, IsScaled);
        ApplyLines(this, MaxLines);
        SetResourceReference(ForegroundProperty, ThemeBrushKey.For(ColorToken.Text));
    }

    /// <summary>Creates a one-line text of <paramref name="style"/> in <paramref name="ink"/>.</summary>
    public StyledText(string text, TextStyleToken style, ColorToken ink = ColorToken.Text)
        : this()
    {
        Text = text;
        TextStyle = style;
        Ink = ink;
    }

    /// <summary>The style of the type scale (<see cref="TextStyleToken.Body"/> by default).</summary>
    public TextStyleToken TextStyle
    {
        get => (TextStyleToken)GetValue(TextStyleProperty);
        set => SetValue(TextStyleProperty, value);
    }

    /// <summary>The color token of the text (<see cref="ColorToken.Text"/> by default).</summary>
    public ColorToken Ink
    {
        get => (ColorToken)GetValue(InkProperty);
        set => SetValue(InkProperty, value);
    }

    /// <summary>
    /// How many lines the text may take before it ends in «…»: 1 (the default) or 2; 0 wraps without limit
    /// (paragraphs and descriptions that are the content itself).
    /// </summary>
    public int MaxLines
    {
        get => (int)GetValue(MaxLinesProperty);
        set => SetValue(MaxLinesProperty, value);
    }

    /// <summary>Whether the size follows the person's text scale (CUA-011).</summary>
    public bool IsScaled
    {
        get => (bool)GetValue(IsScaledProperty);
        set => SetValue(IsScaledProperty, value);
    }

    /// <summary>
    /// Gives <paramref name="block"/> the font, weight, size and line height of <paramref name="style"/> as theme
    /// resources (any <see cref="TextBlock"/>, such as the one a content presenter makes of a string).
    /// </summary>
    public static void Apply(TextBlock block, TextStyleToken style, bool scaled = false)
    {
        ArgumentNullException.ThrowIfNull(block);
        var spec = TextStyles.Get(style);
        block.SetResourceReference(FontFamilyProperty, ThemeKeys.FontFamily(style));
        block.SetResourceReference(FontSizeProperty, ThemeKeys.FontSize(style, scaled));
        block.SetResourceReference(LineHeightProperty, ThemeKeys.LineHeight(style, scaled));
        block.FontWeight = Weight(spec.Weight);
        block.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
    }

    /// <summary>The bundled face of an OpenType weight (400, 500 for keys, 700).</summary>
    public static FontWeight Weight(int weight) =>
        weight switch
        {
            >= 700 => FontWeights.Bold,
            500 => AppFonts.MonoWeight,
            _ => FontWeights.Normal,
        };

    private static void ApplyLines(TextBlock block, int lines)
    {
        block.TextWrapping = lines == 1 ? TextWrapping.NoWrap : TextWrapping.Wrap;
        block.TextTrimming = lines == 0 ? TextTrimming.None : TextTrimming.CharacterEllipsis;
    }

    private static void OnLookChanged(DependencyObject text, DependencyPropertyChangedEventArgs e)
    {
        var block = (StyledText)text;
        Apply(block, block.TextStyle, block.IsScaled);
    }

    private static void OnInkChanged(DependencyObject text, DependencyPropertyChangedEventArgs e) =>
        ((StyledText)text).SetResourceReference(
            ForegroundProperty,
            ThemeBrushKey.For((ColorToken)e.NewValue)
        );

    private static void OnMaxLinesChanged(DependencyObject text, DependencyPropertyChangedEventArgs e)
    {
        ApplyLines((StyledText)text, (int)e.NewValue);
        OnLinesChanged(text, e);
    }

    /// <summary>Two lines at most: the box stops at the last line, which then ends in «…».</summary>
    private static void OnLinesChanged(DependencyObject text, DependencyPropertyChangedEventArgs e)
    {
        var block = (StyledText)text;
        block.MaxHeight =
            block.MaxLines > 1 && !double.IsNaN(block.LineHeight)
                ? block.LineHeight * block.MaxLines
                : double.PositiveInfinity;
    }
}
