using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Workspace.Internal;

namespace Clicalo.UI.Wpf.Workspace.Welcome;

/// <summary>
/// The logo of Clícalo (TEM-008): the accent square with the hand of three strokes, and the word with the «ı» whose
/// accent is an accent-colored stroke, as in the prototype. Used by the welcome and «Acerca de y contacto».
/// </summary>
internal static class BrandMark
{
    private const char AccentedI = 'í';
    private const char DotlessI = 'ı';

    /// <summary>The square logo of <paramref name="size"/> logical pixels; decorative (no name).</summary>
    /// <param name="size">Its side.</param>
    public static Canvas Mark(double size)
    {
        var scale = size / 48;
        var mark = new Canvas
        {
            Width = size,
            Height = size,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var square = new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(13 * scale),
        };
        Ui.Ink(square, Border.BackgroundProperty, ColorToken.Accent);
        mark.Children.Add(square);
        mark.Children.Add(Stroke(20, 20, 8, 19, 0, 1, scale));
        mark.Children.Add(Stroke(23, 6, 7, 12, 32, 1, scale));
        mark.Children.Add(Stroke(33, 8, 4, 6, 72, 0.75, scale));
        return mark;
    }

    /// <summary>
    /// The word <paramref name="name"/> at <paramref name="px"/>, bold, with its «í» drawn as «ı» and an accent stroke;
    /// a name without «í» is drawn as it is. UI Automation reads it as one text, <paramref name="name"/>.
    /// </summary>
    /// <param name="name">[appName].</param>
    /// <param name="px">The type size.</param>
    public static FrameworkElement Word(string name, double px)
    {
        ArgumentNullException.ThrowIfNull(name);
        var accent = name.IndexOf(AccentedI, StringComparison.Ordinal);
        if (accent < 0)
        {
            return Ui.Text(name, px, bold: true);
        }

        var dotless = Ui.Text(new string(DotlessI, 1), px, bold: true);
        var stroke = new Rectangle
        {
            Width = px * 0.13,
            Height = px * 0.34,
            RadiusX = px * 0.07,
            RadiusY = px * 0.07,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new RotateTransform(30),
            IsHitTestVisible = false,
        };
        Ui.Ink(stroke, Shape.FillProperty, ColorToken.Accent);
        var letter = new Grid { ClipToBounds = false };
        letter.Children.Add(dotless);
        letter.Children.Add(stroke);
        dotless.Loaded += (_, _) =>
            stroke.Margin = new Thickness(
                Math.Max(0, (dotless.ActualWidth * 0.48) - (stroke.Width * 0.3)),
                px * 0.1,
                0,
                0
            );
        var word = new BrandWord(name)
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };
        word.Children.Add(Ui.Text(name[..accent], px, bold: true));
        word.Children.Add(letter);
        word.Children.Add(Ui.Text(name[(accent + 1)..], px, bold: true));
        return word;
    }

    /// <summary>The round avatar with the initials of the signature (ACE-001, BIE-004).</summary>
    /// <param name="initials">[creatorInitials].</param>
    /// <param name="size">Its diameter.</param>
    /// <param name="px">The type size of the initials.</param>
    public static Border Avatar(string initials, double size, double px)
    {
        var text = Ui.Text(initials, px, bold: true, ink: ColorToken.Accent);
        text.HorizontalAlignment = HorizontalAlignment.Center;
        var avatar = new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size / 2),
            Child = text,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Ui.Ink(avatar, Border.BackgroundProperty, ColorToken.AccentWash);
        return avatar;
    }

    private static Rectangle Stroke(
        double left,
        double top,
        double width,
        double height,
        double angle,
        double opacity,
        double scale
    )
    {
        var stroke = new Rectangle
        {
            Width = width * scale,
            Height = height * scale,
            RadiusX = width * scale / 2,
            RadiusY = width * scale / 2,
            Opacity = opacity,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new RotateTransform(angle),
        };
        Ui.Ink(stroke, Shape.FillProperty, ColorToken.OnAccent);
        Canvas.SetLeft(stroke, left * scale);
        Canvas.SetTop(stroke, top * scale);
        return stroke;
    }
}
