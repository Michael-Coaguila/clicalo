using System.Globalization;

namespace Clicalo.Generators.Tests.Tokens;

/// <summary>
/// Minimal stand-ins for the WPF types the generated code uses, so the output can be compiled and executed by a
/// portable test host. The real compilation against WPF happens when Clicalo.UI.Wpf builds.
/// </summary>
internal static class WpfStubs
{
    /// <summary>System color names of the stub, in the order of their distinct test values (0x10, 0x11, …).</summary>
    public static readonly string[] SystemColorNames =
    [
        "ActiveBorderColor",
        "ActiveCaptionColor",
        "ActiveCaptionTextColor",
        "AppWorkspaceColor",
        "ControlColor",
        "ControlDarkColor",
        "ControlDarkDarkColor",
        "ControlLightColor",
        "ControlLightLightColor",
        "ControlTextColor",
        "DesktopColor",
        "GradientActiveCaptionColor",
        "GradientInactiveCaptionColor",
        "GrayTextColor",
        "HighlightColor",
        "HighlightTextColor",
        "HotTrackColor",
        "InactiveBorderColor",
        "InactiveCaptionColor",
        "InactiveCaptionTextColor",
        "InfoColor",
        "InfoTextColor",
        "MenuBarColor",
        "MenuColor",
        "MenuHighlightColor",
        "MenuTextColor",
        "ScrollBarColor",
        "WindowColor",
        "WindowFrameColor",
        "WindowTextColor",
    ];

    public static string Source { get; } = BuildSource();

    /// <summary>The <c>#AARRGGBB</c> the stub returns for a system color.</summary>
    public static string SystemColorHex(string name)
    {
        var value = 0x10 + Array.IndexOf(SystemColorNames, name);
        return "#FF"
            + string.Concat(
                Enumerable.Repeat(value.ToString("X2", CultureInfo.InvariantCulture), 3)
            );
    }

    private static string BuildSource()
    {
        var systemColors = string.Concat(
            SystemColorNames.Select(
                (name, i) =>
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"        public static Media.Color {name} => Media.Color.FromArgb(255, {0x10 + i}, {0x10 + i}, {0x10 + i});\n"
                    )
            )
        );
        return """
                namespace System.Windows.Media
                {
                    public readonly struct Color
                    {
                        private Color(byte a, byte r, byte g, byte b) { A = a; R = r; G = g; B = b; }
                        public byte A { get; }
                        public byte R { get; }
                        public byte G { get; }
                        public byte B { get; }
                        public static Color FromArgb(byte a, byte r, byte g, byte b) => new(a, r, g, b);
                        public override string ToString() =>
                            "#" + A.ToString("X2") + R.ToString("X2") + G.ToString("X2") + B.ToString("X2");
                    }

                    public sealed class SolidColorBrush
                    {
                        public SolidColorBrush(Color color) => Color = color;
                        public Color Color { get; }
                        public bool IsFrozen { get; private set; }
                        public void Freeze() => IsFrozen = true;
                    }
                }

                namespace System.Windows
                {
                    public static class SystemColors
                    {

                """
            + systemColors
            + """
                    }
                }

                """;
    }
}
