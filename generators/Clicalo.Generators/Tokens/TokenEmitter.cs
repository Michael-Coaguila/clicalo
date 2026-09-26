using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Clicalo.Design.Math;

namespace Clicalo.Generators.Tokens;

/// <summary>
/// Emits <c>Clicalo.UI.Wpf.Theming.Generated</c>: color tokens as <c>System.Windows.Media.Color</c> per theme with
/// factories of frozen <c>SolidColorBrush</c> (frozen Freezables can be shared between dispatchers, blueprint §3.2
/// rule 8), the list of themes, the Windows contrast-theme map, shapes and motion. Output is deterministic.
/// </summary>
internal static class TokenEmitter
{
    public const string Namespace = "Clicalo.UI.Wpf.Theming.Generated";

    private const string Color = "global::System.Windows.Media.Color";
    private const string Brush = "global::System.Windows.Media.SolidColorBrush";
    private const string SystemColors = "global::System.Windows.SystemColors";
    private const string OutOfRange = "global::System.ArgumentOutOfRangeException";

    public static IReadOnlyList<TokenSource> Emit(TokenModel model) =>
        [
            new("ColorToken.g.cs", EmitColorToken(model)),
            new("CategoryToken.g.cs", EmitCategoryToken(model)),
            new("ThemePalette.g.cs", EmitThemePalette(model)),
            new("ThemePalettes.g.cs", EmitThemePalettes(model)),
            new("SystemHighContrastPalette.g.cs", EmitSystemPalette(model)),
            new("DesignShapes.g.cs", EmitShapes(model)),
            new("Motion.g.cs", EmitMotion(model)),
        ];

    private static string EmitColorToken(TokenModel model)
    {
        var w = new CodeWriter();
        w.Summary(
            "A color token of <c>data/tokens</c>. Every theme defines every token; values are listed as rendered."
        );
        w.Line("public enum ColorToken");
        w.Open();
        for (var i = 0; i < model.ColorTokens.Count; i++)
        {
            var key = model.ColorTokens[i];
            var doc = new StringBuilder("<c>").Append(key).Append("</c>");
            foreach (var theme in model.Themes)
            {
                var color = theme.Colors[key];
                doc.Append(" · ").Append(theme.Key).Append(' ').Append(color.Value.ToArgbHex());
                doc.Append(" (").Append(Xml(color.Source)).Append(')');
                if (color.Note is not null)
                {
                    doc.Append(" [").Append(Xml(color.Note)).Append(']');
                }
            }

            w.Summary(doc.ToString());
            w.Line(JsonShape.ToPascalCase(key) + " = " + Int(i) + ",");
            if (i < model.ColorTokens.Count - 1)
            {
                w.Blank();
            }
        }

        w.Close();
        return w.ToString();
    }

    private static string EmitCategoryToken(TokenModel model)
    {
        var w = new CodeWriter();
        w.Summary(
            "A shortcut category (CAT, TEM-003): its hue gives the icon tint and the active-state wash."
        );
        w.Line("public enum CategoryToken");
        w.Open();
        for (var i = 0; i < model.Categories.Count; i++)
        {
            var key = model.Categories[i];
            var doc = new StringBuilder("<c>").Append(key).Append("</c>");
            foreach (var theme in model.Themes)
            {
                doc.Append(" · ")
                    .Append(theme.Key)
                    .Append(" tint ")
                    .Append(theme.CategoryTints[key].Value.ToArgbHex());
                doc.Append(", wash ").Append(theme.CategoryWashes[key].Value.ToArgbHex());
            }

            w.Summary(doc.ToString());
            w.Line(JsonShape.ToPascalCase(key) + " = " + Int(i) + ",");
            if (i < model.Categories.Count - 1)
            {
                w.Blank();
            }
        }

        w.Close();
        return w.ToString();
    }

    private static string EmitThemePalette(TokenModel model)
    {
        var w = new CodeWriter();
        w.Summary(
            "The colors of one theme as rendered: 8-bit sRGB, OKLCH gamut-mapped with CSS Color 4. Immutable and "
                + "thread-safe; brushes are created frozen so they can be shared between dispatchers."
        );
        w.Line("public sealed class ThemePalette");
        w.Open();
        w.Line("private readonly " + Color + "[] _colors;");
        w.Line("private readonly " + Color + "[] _categoryTints;");
        w.Line("private readonly " + Color + "[] _categoryWashes;");
        w.Blank();
        w.Line("internal ThemePalette(");
        w.Line("    string key,");
        w.Line("    string name,");
        w.Line("    bool isHighContrast,");
        w.Line("    double borderThickness,");
        w.Line("    " + Color + "[] colors,");
        w.Line("    " + Color + "[] categoryTints,");
        w.Line("    " + Color + "[] categoryWashes)");
        w.Open();
        w.Line("if (colors.Length != " + Int(model.ColorTokens.Count) + ")");
        w.Open();
        w.Line(
            "throw new global::System.ArgumentException(\"One color per ColorToken is required.\", nameof(colors));"
        );
        w.Close();
        w.Blank();
        w.Line(
            "if (categoryTints.Length != "
                + Int(model.Categories.Count)
                + " || categoryWashes.Length != "
                + Int(model.Categories.Count)
                + ")"
        );
        w.Open();
        w.Line(
            "throw new global::System.ArgumentException(\"One tint and one wash per CategoryToken are required.\", nameof(categoryTints));"
        );
        w.Close();
        w.Blank();
        w.Line("Key = key;");
        w.Line("Name = name;");
        w.Line("IsHighContrast = isHighContrast;");
        w.Line("BorderThickness = borderThickness;");
        w.Line("_colors = colors;");
        w.Line("_categoryTints = categoryTints;");
        w.Line("_categoryWashes = categoryWashes;");
        w.Close();
        w.Blank();
        w.Summary("Key of the theme in <c>data/tokens</c> (for example <c>dark</c>).");
        w.Line("public string Key { get; }");
        w.Blank();
        w.Summary("C# name of the theme (for example <c>Dark</c>).");
        w.Line("public string Name { get; }");
        w.Blank();
        w.Summary(
            "True for high-contrast themes: opaque surfaces, no blur, thicker borders (TEM-004)."
        );
        w.Line("public bool IsHighContrast { get; }");
        w.Blank();
        w.Summary("Thickness of the <c>border</c> token, in device-independent pixels.");
        w.Line("public double BorderThickness { get; }");
        foreach (var key in model.ColorTokens)
        {
            w.Blank();
            w.Summary("The <c>" + key + "</c> color of this theme.");
            w.Line(
                "public "
                    + Color
                    + " "
                    + JsonShape.ToPascalCase(key)
                    + " => _colors[(int)ColorToken."
                    + JsonShape.ToPascalCase(key)
                    + "];"
            );
        }

        w.Blank();
        w.Summary("The color of <paramref name=\"token\"/> in this theme.");
        w.Line("public " + Color + " GetColor(ColorToken token) => _colors[Index(token)];");
        w.Blank();
        w.Summary("The icon tint of <paramref name=\"category\"/> in this theme.");
        w.Line(
            "public "
                + Color
                + " GetCategoryTint(CategoryToken category) => _categoryTints[Index(category)];"
        );
        w.Blank();
        w.Summary("The active-state background of <paramref name=\"category\"/> in this theme.");
        w.Line(
            "public "
                + Color
                + " GetCategoryWash(CategoryToken category) => _categoryWashes[Index(category)];"
        );
        w.Blank();
        w.Summary(
            "A new frozen brush of <paramref name=\"token\"/>, safe to share between threads."
        );
        w.Line(
            "public "
                + Brush
                + " CreateBrush(ColorToken token) => CreateFrozenBrush(GetColor(token));"
        );
        w.Blank();
        w.Summary("A new frozen brush of the icon tint of <paramref name=\"category\"/>.");
        w.Line(
            "public "
                + Brush
                + " CreateCategoryTintBrush(CategoryToken category) => CreateFrozenBrush(GetCategoryTint(category));"
        );
        w.Blank();
        w.Summary(
            "A new frozen brush of the active-state background of <paramref name=\"category\"/>."
        );
        w.Line(
            "public "
                + Brush
                + " CreateCategoryWashBrush(CategoryToken category) => CreateFrozenBrush(GetCategoryWash(category));"
        );
        w.Blank();
        w.Summary("A new frozen brush of <paramref name=\"color\"/>.");
        w.Line("public static " + Brush + " CreateFrozenBrush(" + Color + " color)");
        w.Open();
        w.Line("var brush = new " + Brush + "(color);");
        w.Line("brush.Freeze();");
        w.Line("return brush;");
        w.Close();
        w.Blank();
        w.Line("/// <inheritdoc/>");
        w.Line("public override string ToString() => Name;");
        w.Blank();
        w.Line(
            "private static int Index(ColorToken token) => (uint)token < "
                + Int(model.ColorTokens.Count)
                + "u ? (int)token : throw new "
                + OutOfRange
                + "(nameof(token), token, null);"
        );
        w.Blank();
        w.Line(
            "private static int Index(CategoryToken category) => (uint)category < "
                + Int(model.Categories.Count)
                + "u ? (int)category : throw new "
                + OutOfRange
                + "(nameof(category), category, null);"
        );
        w.Close();
        return w.ToString();
    }

    private static string EmitThemePalettes(TokenModel model)
    {
        var w = new CodeWriter();
        w.Summary("Every theme of <c>data/tokens/theme-palettes.json</c>, in data order.");
        w.Line("public static class ThemePalettes");
        w.Open();
        foreach (var theme in model.Themes)
        {
            w.Summary("The <c>" + theme.Key + "</c> theme.");
            w.Line("public static ThemePalette " + theme.Name + " { get; } =");
            w.Indent();
            w.Line("new(");
            w.Indent();
            w.Line(Str(theme.Key) + ",");
            w.Line(Str(theme.Name) + ",");
            w.Line((theme.IsHighContrast ? "true" : "false") + ",");
            w.Line(Dbl(theme.BorderThickness) + ",");
            ColorArray(w, model.ColorTokens, theme.Colors, ",");
            ColorArray(w, model.Categories, theme.CategoryTints, ",");
            ColorArray(w, model.Categories, theme.CategoryWashes, string.Empty);
            w.Outdent();
            w.Line(");");
            w.Outdent();
            w.Blank();
        }

        w.Summary("All themes, in data order.");
        var names = new List<string>();
        foreach (var theme in model.Themes)
        {
            names.Add(theme.Name);
        }

        w.Line(
            "public static global::System.Collections.Generic.IReadOnlyList<ThemePalette> All { get; } = ["
                + string.Join(", ", names)
                + "];"
        );
        w.Blank();
        w.Line("private static " + Color + " Argb(uint value) =>");
        w.Line(
            "    "
                + Color
                + ".FromArgb((byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value);"
        );
        w.Close();
        return w.ToString();
    }

    private static void ColorArray(
        CodeWriter w,
        IReadOnlyList<string> keys,
        Dictionary<string, ResolvedColor> colors,
        string suffix
    )
    {
        w.Line("[");
        w.Indent();
        foreach (var key in keys)
        {
            var color = colors[key];
            w.Line(
                "Argb(0x"
                    + Hex(color.Value)
                    + "), // "
                    + key
                    + ": "
                    + color.Source.Replace("\r", " ").Replace("\n", " ")
            );
        }

        w.Outdent();
        w.Line("]" + suffix);
    }

    private static string EmitSystemPalette(TokenModel model)
    {
        var borderThickness = 1d;
        foreach (var theme in model.Themes)
        {
            if (theme.IsHighContrast)
            {
                borderThickness = System.Math.Max(borderThickness, theme.BorderThickness);
            }
        }

        var w = new CodeWriter();
        w.Summary(
            "The Windows contrast theme mapped onto the tokens (<c>hc-system-map.json</c>, TEM-001). Values are read "
                + "from <c>SystemColors</c> when called: capture a new palette after <c>WM_SYSCOLORCHANGE</c>."
        );
        w.Line("public static class SystemHighContrastPalette");
        w.Open();
        w.Summary("Key reported by captured palettes.");
        w.Line("public const string Key = \"system\";");
        w.Blank();
        w.Summary("The current system color that replaces <paramref name=\"token\"/>.");
        w.Line("public static " + Color + " GetColor(ColorToken token) =>");
        w.Indent();
        w.Line("token switch");
        w.Line("{");
        w.Indent();
        foreach (var key in model.ColorTokens)
        {
            w.Line(
                "ColorToken."
                    + JsonShape.ToPascalCase(key)
                    + " => "
                    + SystemColors
                    + "."
                    + model.SystemColorByToken[key]
                    + ","
            );
        }

        w.Line("_ => throw new " + OutOfRange + "(nameof(token), token, null),");
        w.Outdent();
        w.Line("};");
        w.Outdent();
        w.Blank();
        w.Summary(
            "The current system color of every category tint (categories are told apart by icon and text)."
        );
        w.Line(
            "public static "
                + Color
                + " CategoryTint => "
                + SystemColors
                + "."
                + model.SystemCategoryTint
                + ";"
        );
        w.Blank();
        w.Summary("The current system color of every category wash.");
        w.Line(
            "public static "
                + Color
                + " CategoryWash => "
                + SystemColors
                + "."
                + model.SystemCategoryWash
                + ";"
        );
        w.Blank();
        w.Summary("Snapshots the current system colors as an immutable palette.");
        w.Line("public static ThemePalette Capture()");
        w.Open();
        w.Line("var colors = new " + Color + "[" + Int(model.ColorTokens.Count) + "];");
        w.Line("for (var i = 0; i < colors.Length; i++)");
        w.Open();
        w.Line("colors[i] = GetColor((ColorToken)i);");
        w.Close();
        w.Blank();
        w.Line("var tints = new " + Color + "[" + Int(model.Categories.Count) + "];");
        w.Line("var washes = new " + Color + "[tints.Length];");
        w.Line("global::System.Array.Fill(tints, CategoryTint);");
        w.Line("global::System.Array.Fill(washes, CategoryWash);");
        w.Line(
            "return new ThemePalette(Key, \"SystemHighContrast\", true, "
                + Dbl(borderThickness)
                + ", colors, tints, washes);"
        );
        w.Close();
        w.Close();
        return w.ToString();
    }

    private static string EmitShapes(TokenModel model)
    {
        var w = new CodeWriter();
        w.Summary("Corner radii in device-independent pixels (docs/07 «Formas»).");
        w.Line("public static class Radii");
        w.Open();
        for (var i = 0; i < model.Radii.Count; i++)
        {
            w.Summary("<c>" + model.Radii[i].Key + "</c>.");
            w.Line(
                "public const double "
                    + JsonShape.ToPascalCase(model.Radii[i].Key)
                    + " = "
                    + Dbl(model.Radii[i].Value)
                    + ";"
            );
            if (i < model.Radii.Count - 1)
            {
                w.Blank();
            }
        }

        w.Close();
        w.Blank();
        w.Summary(
            "The focus ring of every control (TEM-009): drawn in the <c>focusRing</c> color, outside the control."
        );
        w.Line("public static class FocusRing");
        w.Open();
        w.Summary("Stroke thickness in device-independent pixels.");
        w.Line("public const double Thickness = " + Dbl(model.FocusRingThickness) + ";");
        w.Blank();
        w.Summary("Gap between the control and the ring, in device-independent pixels.");
        w.Line("public const double Offset = " + Dbl(model.FocusRingOffset) + ";");
        w.Close();
        w.Blank();
        w.Summary("A precomputed drop shadow (never <c>DropShadowEffect</c>, blueprint §8.1).");
        w.Line(
            "/// <param name=\"OffsetX\">Horizontal offset in device-independent pixels.</param>"
        );
        w.Line("/// <param name=\"OffsetY\">Vertical offset in device-independent pixels.</param>");
        w.Line("/// <param name=\"BlurRadius\">Blur radius in device-independent pixels.</param>");
        w.Line(
            "/// <param name=\"Opacity\">Multiplies the alpha of the theme's <c>shadow</c> color.</param>"
        );
        w.Line(
            "public readonly record struct ShadowSpec(double OffsetX, double OffsetY, double BlurRadius, double Opacity)"
        );
        w.Open();
        if (model.ColorTokens.Contains("shadow"))
        {
            w.Summary(
                "The shadow color of <paramref name=\"palette\"/> with this elevation's opacity applied; fully "
                    + "transparent in high contrast, which has no translucency or blur (TEM-004)."
            );
            w.Line("public " + Color + " ColorIn(ThemePalette palette)");
            w.Open();
            w.Line("global::System.ArgumentNullException.ThrowIfNull(palette);");
            w.Line("var color = palette.Shadow;");
            w.Line("if (palette.IsHighContrast)");
            w.Open();
            w.Line("return " + Color + ".FromArgb(0, color.R, color.G, color.B);");
            w.Close();
            w.Blank();
            w.Line(
                "return "
                    + Color
                    + ".FromArgb((byte)global::System.Math.Round(color.A * Opacity, global::System.MidpointRounding.AwayFromZero), color.R, color.G, color.B);"
            );
            w.Close();
        }

        w.Close();
        w.Blank();
        w.Summary("Elevation shadows (docs/07 «Formas»).");
        w.Line("public static class Shadows");
        w.Open();
        for (var i = 0; i < model.Shadows.Count; i++)
        {
            var shadow = model.Shadows[i];
            w.Summary("<c>" + shadow.Key + "</c>.");
            w.Line(
                "public static ShadowSpec "
                    + JsonShape.ToPascalCase(shadow.Key)
                    + " { get; } = new("
                    + Dbl(shadow.OffsetX)
                    + ", "
                    + Dbl(shadow.OffsetY)
                    + ", "
                    + Dbl(shadow.Blur)
                    + ", "
                    + Dbl(shadow.Opacity)
                    + ");"
            );
            if (i < model.Shadows.Count - 1)
            {
                w.Blank();
            }
        }

        w.Close();
        return w.ToString();
    }

    private static string EmitMotion(TokenModel model)
    {
        var w = new CodeWriter();
        w.Summary("A motion duration of <c>data/tokens/motion.json</c> (TEM-006).");
        w.Line("public enum MotionToken");
        w.Open();
        for (var i = 0; i < model.Motion.Count; i++)
        {
            var motion = model.Motion[i];
            w.Summary(
                Xml(motion.Use)
                    + " "
                    + Int(motion.Milliseconds)
                    + " ms; "
                    + Int(motion.ReducedMilliseconds)
                    + " ms with reduced motion."
            );
            w.Line(JsonShape.ToPascalCase(motion.Key) + " = " + Int(i) + ",");
            if (i < model.Motion.Count - 1)
            {
                w.Blank();
            }
        }

        w.Close();
        w.Blank();
        w.Summary(
            "Durations with reduced motion applied (own setting or Windows animations off, TEM-006)."
        );
        w.Line("public static class Motion");
        w.Open();
        w.Summary("The duration of <paramref name=\"token\"/>, or its reduced-motion value.");
        w.Line(
            "public static global::System.TimeSpan Get(MotionToken token, bool reduceMotion) =>"
        );
        w.Indent();
        w.Line("global::System.TimeSpan.FromMilliseconds(");
        w.Indent();
        w.Line("token switch");
        w.Line("{");
        w.Indent();
        foreach (var motion in model.Motion)
        {
            w.Line(
                "MotionToken."
                    + JsonShape.ToPascalCase(motion.Key)
                    + " => reduceMotion ? "
                    + Int(motion.ReducedMilliseconds)
                    + " : "
                    + Int(motion.Milliseconds)
                    + ","
            );
        }

        w.Line("_ => throw new " + OutOfRange + "(nameof(token), token, null),");
        w.Outdent();
        w.Line("}");
        w.Outdent();
        w.Line(");");
        w.Outdent();
        w.Close();
        return w.ToString();
    }

    private static string Hex(Rgba8 color) => color.ToArgbHex().Substring(1);

    private static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Dbl(double value)
    {
        var text = value.ToString("R", CultureInfo.InvariantCulture);
        return text.IndexOf('.') >= 0 || text.IndexOf('E') >= 0 ? text : text + "d";
    }

    private static string Str(string value) =>
        "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    private static string Xml(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    /// <summary>Minimal indented writer; every file gets the auto-generated header and the namespace.</summary>
    private sealed class CodeWriter
    {
        private readonly StringBuilder _text = new();
        private int _indent;

        public CodeWriter()
        {
            Line("// <auto-generated/>");
            Line(
                "// Generated by Clicalo.Generators (TokenGenerator) from data/tokens. Do not edit: change the data."
            );
            Line("#nullable enable");
            Blank();
            Line("namespace " + Namespace + ";");
            Blank();
        }

        public void Line(string text) => _text.Append(' ', _indent * 4).Append(text).Append('\n');

        public void Blank() => _text.Append('\n');

        public void Open()
        {
            Line("{");
            _indent++;
        }

        public void Close()
        {
            _indent--;
            Line("}");
        }

        public void Indent() => _indent++;

        public void Outdent() => _indent--;

        public void Summary(string text) => Line("/// <summary>" + text + "</summary>");

        public override string ToString() => _text.ToString();
    }
}
