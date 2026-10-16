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

    /// <summary>With <see cref="Variable"/>, also saves the states audited with the largest text (any value).</summary>
    public const string LargeTextVariable = "CLICALO_GALLERY_LARGE_TEXT";

    /// <summary>The scale of the person's screen (2400 × 1600 at 175 %).</summary>
    public const double Scale = 1.75;

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
    /// With <see cref="LargeTextVariable"/> also set, a state audited with the largest text (150 %, CUA-011) is saved as
    /// it is, in its own theme, as <c>{name}.150-light.png</c> or <c>{name}.150-contrast.png</c>, outside the index:
    /// the look of a redesign is checked with the text the person may choose too.
    /// </summary>
    private static void SaveLargeText(AuditHost host, string group, string name)
    {
        if (
            Folder is not { } folder
            || Environment.GetEnvironmentVariable(LargeTextVariable) is not { Length: > 0 }
            || host.Theme is not { } theme
            || theme.TextScalePercent != TypeScale.MaxScalePercent
            || host.Root.ActualWidth <= 0
            || host.Root.ActualHeight <= 0
        )
        {
            return;
        }

        var look = theme.Preference == ThemeChoice.HighContrast ? "contrast" : "light";
        name = name.Replace(", LightLargeText)", ")", StringComparison.Ordinal)
            .Replace(", ContrastLargeText)", ")", StringComparison.Ordinal)
            .Replace(" (LightLargeText)", string.Empty, StringComparison.Ordinal)
            .Replace(" (ContrastLargeText)", string.Empty, StringComparison.Ordinal);
        lock (Gate)
        {
            Directory.CreateDirectory(folder);
            host.LayOut();
            File.WriteAllBytes(
                Path.Combine(folder, Slug(group) + "--" + Slug(name) + ".150-" + look + ".png"),
                Render(host)
            );
        }
    }

    private static byte[] Render(AuditHost host)
    {
        var root = host.Root;
        var dpi = 96 * Scale;
        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(root.ActualWidth * Scale),
            (int)Math.Ceiling(root.ActualHeight * Scale),
            dpi,
            dpi,
            PixelFormats.Pbgra32
        );
        if (host.Window is { } window)
        {
            bitmap.Render(Frame(window, new Rect(0, 0, root.ActualWidth, root.ActualHeight)));
        }

        bitmap.Render(root);
        bitmap.Freeze();
        return PngCodec.Encode(bitmap);
    }

    /// <summary>
    /// What the window paints behind its content: its background and its border, with the rounded shape of a surface
    /// (<see cref="NonActivatingWindow.Look"/>), as its frame does on screen.
    /// </summary>
    private static DrawingVisual Frame(Window window, Rect bounds)
    {
        var visual = new DrawingVisual();
        var corners = new CornerRadius(0);
        if (window is NonActivatingWindow { Look: { } look })
        {
            var half = Math.Min(bounds.Width, bounds.Height) / 2;
            corners = look.Round ? new CornerRadius(half) : look.Corners;
        }

        var thickness = window.BorderThickness.Left;
        var pen =
            window.BorderBrush is { } border && thickness > 0 ? new Pen(border, thickness) : null;
        var inset = thickness / 2;
        using var context = visual.RenderOpen();
        context.DrawGeometry(
            window.Background,
            pen,
            Rounded(
                new Rect(
                    bounds.X + inset,
                    bounds.Y + inset,
                    Math.Max(0, bounds.Width - thickness),
                    Math.Max(0, bounds.Height - thickness)
                ),
                corners
            )
        );
        return visual;
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
