using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Clicalo.Domain.Settings;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Windowing;

namespace Clicalo.Windowing.IntegrationTests.Automation.Audit;

/// <summary>
/// The gallery of the real surfaces (docs/architecture/tooling.md, «Galería de superficies»): with the environment
/// variable <see cref="Variable"/> set to a folder, every state the audit reaches as drawn (dark theme, text at 100 %)
/// is saved as PNG at the person's scale (<see cref="Scale"/>), in the dark, the light and the high contrast theme, and
/// <c>index.html</c> shows them grouped and named. Without the variable it does nothing, so <c>cl check</c> never
/// writes an image. It only renders the hidden presentation source of <see cref="AuditHost"/>: nothing is captured
/// from the screen.
/// </summary>
internal static class AuditGallery
{
    /// <summary>The environment variable with the folder of the gallery.</summary>
    public const string Variable = "CLICALO_GALLERY";

    /// <summary>The scale of the person's screen (2400 × 1600 at 175 %).</summary>
    public const double Scale = 1.75;

    /// <summary>
    /// The height of the work area of the person's screen, in DIPs: 1600 / 1.75, less the 48 of the taskbar.
    /// </summary>
    public const double WorkAreaHeight = 866;

    /// <summary>
    /// The tallest Quick settings may be on the person's screen (AJR-001, D21): the work area less
    /// <see cref="Space.S2"/> above and below, the rule of the surface set (<c>SurfaceSet.QuickMaxHeight</c>), which the
    /// audit, with no monitor, cannot ask.
    /// </summary>
    public const double QuickRoom = WorkAreaHeight - (2 * Space.S2);

    private const string Manifest = "gallery.tsv";
    private const string Index = "index.html";

    /// <summary>The order of the groups in the index.</summary>
    private static readonly string[] GroupOrder =
    [
        "Sistema de diseño",
        "Panel",
        "Burbuja y Soltar todo",
        "Pestaña",
        "Centro de control",
        "Bienvenida",
    ];

    private static readonly Lock Gate = new();

    /// <summary>The folder of the gallery, or <see langword="null"/> when it is off.</summary>
    public static string? Folder =>
        Environment.GetEnvironmentVariable(Variable) is { Length: > 0 } folder ? folder : null;

    /// <summary>
    /// Saves the view of <paramref name="host"/> as <paramref name="name"/> in <paramref name="group"/>, when the gallery
    /// is on and the view is drawn as designed (the other looks of the audit are not the person's).
    /// </summary>
    public static void Save(AuditHost host, string group, string name)
    {
        ArgumentNullException.ThrowIfNull(host);
        SaveLargeText(host, group, name);
        if (
            Folder is not { } folder
            || host.Theme is not { } theme
            || theme.Preference != ThemeChoice.Dark
            || theme.TextScalePercent != TypeScale.MinScalePercent
            || host.Root.ActualWidth <= 0
            || host.Root.ActualHeight <= 0
        )
        {
            return;
        }

        // The look is always the one as drawn: it says nothing in the gallery.
        name = name.Replace(", Dark)", ")", StringComparison.Ordinal)
            .Replace(" (Dark)", string.Empty, StringComparison.Ordinal);
        var file = Slug(group) + "--" + Slug(name);
        lock (Gate)
        {
            Directory.CreateDirectory(folder);
            host.LayOut();
            File.WriteAllBytes(Path.Combine(folder, file + ".dark.png"), Render(host));
            theme.Preference = ThemeChoice.Light;
            try
            {
                host.LayOut();
                File.WriteAllBytes(Path.Combine(folder, file + ".light.png"), Render(host));
                theme.Preference = ThemeChoice.HighContrast;
                host.LayOut();
                File.WriteAllBytes(Path.Combine(folder, file + ".contrast.png"), Render(host));
            }
            finally
            {
                theme.Preference = ThemeChoice.Dark;
                host.LayOut();
            }

            Record(folder, group, name, file);
        }
    }

    /// <summary>
    /// Saves a state audited with larger text (CUA-011) as it is drawn, in its own theme, as
    /// <c>{name}.light-150.png</c>, <c>{name}.contrast-150.png</c> or <c>{name}.dark-150.png</c>, outside the index: the
    /// design is checked with the text the person may choose too. Every surface goes through it from
    /// <see cref="Save"/>.
    /// </summary>
    private static void SaveLargeText(AuditHost host, string group, string name)
    {
        if (
            Folder is not { } folder
            || host.Theme is not { } theme
            || theme.TextScalePercent == TypeScale.MinScalePercent
            || host.Root.ActualWidth <= 0
            || host.Root.ActualHeight <= 0
        )
        {
            return;
        }

        var look = theme.Preference switch
        {
            ThemeChoice.HighContrast => "contrast",
            ThemeChoice.Light => "light",
            _ => "dark",
        };
        name = name.Replace(", LightLargeText)", ")", StringComparison.Ordinal)
            .Replace(", ContrastLargeText)", ")", StringComparison.Ordinal)
            .Replace(" (LightLargeText)", string.Empty, StringComparison.Ordinal)
            .Replace(" (ContrastLargeText)", string.Empty, StringComparison.Ordinal);
        lock (Gate)
        {
            Directory.CreateDirectory(folder);
            host.LayOut();
            File.WriteAllBytes(
                Path.Combine(folder, Slug(group) + "--" + Slug(name) + "." + look + "-150.png"),
                Render(host)
            );
        }
    }

    /// <summary>
    /// The view as the window shows it: its border and background around the content, which goes inside the border and
    /// is clipped to the inner rounded shape, as <c>SurfaceFrame</c> does on screen. A window that takes the size of its
    /// content is the content and its border; one with a fixed size keeps it, its border inside. The hosted tree itself
    /// is not touched: the audit measures it as it is.
    /// </summary>
    private static byte[] Render(AuditHost host)
    {
        var root = host.Root;
        var content = new Size(root.ActualWidth, root.ActualHeight);
        var border = host.Window?.BorderThickness ?? default;
        var offset = new Vector(
            host.FixesWidth ? 0 : border.Left,
            host.FixesHeight ? 0 : border.Top
        );
        var outer = new Rect(
            0,
            0,
            content.Width + (host.FixesWidth ? 0 : border.Left + border.Right),
            content.Height + (host.FixesHeight ? 0 : border.Top + border.Bottom)
        );
        var dpi = 96 * Scale;
        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(outer.Width * Scale),
            (int)Math.Ceiling(outer.Height * Scale),
            dpi,
            dpi,
            PixelFormats.Pbgra32
        );
        if (host.Window is { } window)
        {
            bitmap.Render(Framed(window, root, outer, border, offset, content));
        }
        else
        {
            bitmap.Render(root);
        }

        bitmap.Freeze();
        return PngCodec.Encode(bitmap);
    }

    /// <summary>
    /// The window drawn whole: its background inside the border, the content clipped to the inner rounded shape and
    /// the border over its edge, so no layer of the content hides the outline.
    /// </summary>
    private static DrawingVisual Framed(
        Window window,
        Visual root,
        Rect outer,
        Thickness border,
        Vector offset,
        Size content
    )
    {
        var corners = new CornerRadius(0);
        if (window is NonActivatingWindow { Look: { } look })
        {
            corners = look.Round
                ? new CornerRadius(Math.Min(outer.Width, outer.Height) / 2)
                : look.Corners;
        }

        var inner = new Rect(
            outer.X + border.Left,
            outer.Y + border.Top,
            Math.Max(0, outer.Width - border.Left - border.Right),
            Math.Max(0, outer.Height - border.Top - border.Bottom)
        );
        var max = Math.Min(inner.Width, inner.Height) / 2;
        var innerShape = Rounded(
            inner,
            new CornerRadius(
                Inset(corners.TopLeft, Math.Max(border.Left, border.Top), max),
                Inset(corners.TopRight, Math.Max(border.Right, border.Top), max),
                Inset(corners.BottomRight, Math.Max(border.Right, border.Bottom), max),
                Inset(corners.BottomLeft, Math.Max(border.Left, border.Bottom), max)
            )
        );
        var visual = new DrawingVisual();
        using var context = visual.RenderOpen();
        context.DrawGeometry(window.Background, null, innerShape);
        context.PushClip(innerShape);
        var at = new Rect(new Point(offset.X, offset.Y), content);
        context.DrawRectangle(
            new VisualBrush(root)
            {
                Stretch = Stretch.None,
                AlignmentX = AlignmentX.Left,
                AlignmentY = AlignmentY.Top,
                ViewboxUnits = BrushMappingMode.Absolute,
                Viewbox = new Rect(content),
                ViewportUnits = BrushMappingMode.Absolute,
                Viewport = at,
            },
            null,
            at
        );
        context.Pop();
        if (window.BorderBrush is { } brush && !inner.Equals(outer))
        {
            context.DrawGeometry(
                brush,
                null,
                new CombinedGeometry(
                    GeometryCombineMode.Exclude,
                    Rounded(outer, corners),
                    innerShape
                )
            );
        }

        return visual;

        static double Inset(double radius, double thickness, double limit) =>
            Math.Clamp(radius - thickness, 0, limit);
    }

    private static StreamGeometry Rounded(Rect r, CornerRadius c)
    {
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(new Point(r.Left + c.TopLeft, r.Top), isFilled: true, isClosed: true);
            g.LineTo(new Point(r.Right - c.TopRight, r.Top), true, false);
            Arc(g, new Point(r.Right, r.Top + c.TopRight), c.TopRight);
            g.LineTo(new Point(r.Right, r.Bottom - c.BottomRight), true, false);
            Arc(g, new Point(r.Right - c.BottomRight, r.Bottom), c.BottomRight);
            g.LineTo(new Point(r.Left + c.BottomLeft, r.Bottom), true, false);
            Arc(g, new Point(r.Left, r.Bottom - c.BottomLeft), c.BottomLeft);
            g.LineTo(new Point(r.Left, r.Top + c.TopLeft), true, false);
            Arc(g, new Point(r.Left + c.TopLeft, r.Top), c.TopLeft);
        }

        geometry.Freeze();
        return geometry;

        static void Arc(StreamGeometryContext g, Point to, double radius)
        {
            if (radius > 0)
            {
                g.ArcTo(
                    to,
                    new Size(radius, radius),
                    0,
                    false,
                    SweepDirection.Clockwise,
                    true,
                    false
                );
            }
            else
            {
                g.LineTo(to, true, false);
            }
        }
    }

    /// <summary>Adds the case to the manifest (once) and writes the index again from it.</summary>
    private static void Record(string folder, string group, string name, string file)
    {
        var manifest = Path.Combine(folder, Manifest);
        var entries = File.Exists(manifest)
            ? File.ReadAllLines(manifest, Encoding.UTF8)
                .Select(static line => line.Split('\t'))
                .Where(static parts => parts.Length == 3)
                .Select(static parts => (Group: parts[0], Name: parts[1], File: parts[2]))
                .ToList()
            : [];
        if (!entries.Any(entry => string.Equals(entry.File, file, StringComparison.Ordinal)))
        {
            entries.Add((group, name, file));
            File.WriteAllLines(
                manifest,
                entries.Select(static entry => entry.Group + "\t" + entry.Name + "\t" + entry.File),
                Encoding.UTF8
            );
        }

        File.WriteAllText(Path.Combine(folder, Index), Html(entries), Encoding.UTF8);
    }

    private static string Html(IReadOnlyList<(string Group, string Name, string File)> entries)
    {
        var html = new StringBuilder();
        html.Append(
            """
            <!doctype html>
            <html lang="es">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>Galería de Clícalo</title>
            <style>
            :root { color-scheme: dark; --bg: #16181d; --fg: #e8e8ea; --muted: #a0a3ab; --line: #2c2f36; }
            body { margin: 0; padding: 16px; background: var(--bg); color: var(--fg); font: 15px/1.45 "Segoe UI", system-ui, sans-serif; }
            nav { position: sticky; top: 0; background: var(--bg); padding: 8px 0; border-bottom: 1px solid var(--line); z-index: 1; }
            nav a { color: var(--fg); margin-right: 16px; }
            h2 { margin: 32px 0 8px; }
            figure { margin: 0 0 24px; padding: 12px; border: 1px solid var(--line); border-radius: 8px; }
            figcaption { font-weight: 600; margin-bottom: 8px; }
            .pair { display: flex; flex-wrap: wrap; gap: 12px; align-items: flex-start; }
            .pair a { display: block; padding: 12px; border-radius: 6px; }
            .pair a.dark { background: #2a2c31; }
            .pair a.light { background: #d9dbe0; }
            .pair a.contrast { background: #000000; outline: 1px solid #ffffff; }
            .pair img { display: block; max-width: min(100%, 900px); height: auto; outline: 1px dashed #7a7f8a; }
            small { color: var(--muted); }
            </style>
            </head>
            <body>
            <h1>Galería de Clícalo</h1>
            <p><small>Superficies reales, escala 1,75, tema oscuro, claro y alto contraste. Cada imagen enlaza a su tamaño real.</small></p>
            <nav>
            """
        );
        var groups = entries
            .GroupBy(static entry => entry.Group, StringComparer.Ordinal)
            .OrderBy(static group => Order(group.Key))
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .ToList();
        foreach (var group in groups)
        {
            html.Append(
                CultureInfo.InvariantCulture,
                $"<a href=\"#{Slug(group.Key)}\">{Encode(group.Key)} ({group.Count()})</a>"
            );
        }

        html.Append("</nav>\n");
        foreach (var group in groups)
        {
            html.Append(
                CultureInfo.InvariantCulture,
                $"<h2 id=\"{Slug(group.Key)}\">{Encode(group.Key)}</h2>\n"
            );
            foreach (var entry in group.OrderBy(static entry => entry.Name, StringComparer.Ordinal))
            {
                html.Append(
                    CultureInfo.InvariantCulture,
                    $"""
                    <figure id="{entry.File}"><figcaption>{Encode(
                        entry.Name
                    )}</figcaption><div class="pair">
                    <a class="dark" href="{entry.File}.dark.png"><img loading="lazy" src="{entry.File}.dark.png" alt="{Encode(
                        entry.Name
                    )}, tema oscuro"></a>
                    <a class="light" href="{entry.File}.light.png"><img loading="lazy" src="{entry.File}.light.png" alt="{Encode(
                        entry.Name
                    )}, tema claro"></a>
                    <a class="contrast" href="{entry.File}.contrast.png"><img loading="lazy" src="{entry.File}.contrast.png" alt="{Encode(
                        entry.Name
                    )}, alto contraste"></a>
                    </div></figure>

                    """
                );
            }
        }

        html.Append("</body>\n</html>\n");
        return html.ToString();
    }

    private static int Order(string group)
    {
        var index = Array.IndexOf(GroupOrder, group);
        return index < 0 ? GroupOrder.Length : index;
    }

    private static string Encode(string text) => WebUtility.HtmlEncode(text);

    /// <summary>A file name of lower-case letters, digits and dashes.</summary>
    private static string Slug(string text)
    {
        var slug = new StringBuilder(text.Length);
        foreach (var c in text.Normalize(NormalizationForm.FormD))
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                slug.Append(char.ToLowerInvariant(c));
            }
            else if (
                CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark
                && slug.Length > 0
                && slug[^1] != '-'
            )
            {
                slug.Append('-');
            }
        }

        return slug.ToString().Trim('-');
    }
}
