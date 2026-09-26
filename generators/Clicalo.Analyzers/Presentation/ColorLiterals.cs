using System;
using System.Collections.Generic;
using Clicalo.Analyzers.Common;

namespace Clicalo.Analyzers.Presentation;

/// <summary>Recognizes colors written by hand: hexadecimal, <c>sc#</c> and the named colors of WPF.</summary>
internal static class ColorLiterals
{
    private const string TransparentName = "Transparent";

    /// <summary>Last words of the XAML attributes and elements that take a color or a brush.</summary>
    private static readonly IReadOnlyList<string> ColorWords =
    [
        "Background",
        "Foreground",
        "Brush",
        "Color",
        "Fill",
        "Stroke",
    ];

    // System.Windows.Media.Colors, except Transparent, which is the absence of color rather than a design decision.
    private static readonly HashSet<string> NamedColors = new(StringComparer.OrdinalIgnoreCase)
    {
        "AliceBlue",
        "AntiqueWhite",
        "Aqua",
        "Aquamarine",
        "Azure",
        "Beige",
        "Bisque",
        "Black",
        "BlanchedAlmond",
        "Blue",
        "BlueViolet",
        "Brown",
        "BurlyWood",
        "CadetBlue",
        "Chartreuse",
        "Chocolate",
        "Coral",
        "CornflowerBlue",
        "Cornsilk",
        "Crimson",
        "Cyan",
        "DarkBlue",
        "DarkCyan",
        "DarkGoldenrod",
        "DarkGray",
        "DarkGreen",
        "DarkKhaki",
        "DarkMagenta",
        "DarkOliveGreen",
        "DarkOrange",
        "DarkOrchid",
        "DarkRed",
        "DarkSalmon",
        "DarkSeaGreen",
        "DarkSlateBlue",
        "DarkSlateGray",
        "DarkTurquoise",
        "DarkViolet",
        "DeepPink",
        "DeepSkyBlue",
        "DimGray",
        "DodgerBlue",
        "Firebrick",
        "FloralWhite",
        "ForestGreen",
        "Fuchsia",
        "Gainsboro",
        "GhostWhite",
        "Gold",
        "Goldenrod",
        "Gray",
        "Green",
        "GreenYellow",
        "Honeydew",
        "HotPink",
        "IndianRed",
        "Indigo",
        "Ivory",
        "Khaki",
        "Lavender",
        "LavenderBlush",
        "LawnGreen",
        "LemonChiffon",
        "LightBlue",
        "LightCoral",
        "LightCyan",
        "LightGoldenrodYellow",
        "LightGray",
        "LightGreen",
        "LightPink",
        "LightSalmon",
        "LightSeaGreen",
        "LightSkyBlue",
        "LightSlateGray",
        "LightSteelBlue",
        "LightYellow",
        "Lime",
        "LimeGreen",
        "Linen",
        "Magenta",
        "Maroon",
        "MediumAquamarine",
        "MediumBlue",
        "MediumOrchid",
        "MediumPurple",
        "MediumSeaGreen",
        "MediumSlateBlue",
        "MediumSpringGreen",
        "MediumTurquoise",
        "MediumVioletRed",
        "MidnightBlue",
        "MintCream",
        "MistyRose",
        "Moccasin",
        "NavajoWhite",
        "Navy",
        "OldLace",
        "Olive",
        "OliveDrab",
        "Orange",
        "OrangeRed",
        "Orchid",
        "PaleGoldenrod",
        "PaleGreen",
        "PaleTurquoise",
        "PaleVioletRed",
        "PapayaWhip",
        "PeachPuff",
        "Peru",
        "Pink",
        "Plum",
        "PowderBlue",
        "Purple",
        "Red",
        "RosyBrown",
        "RoyalBlue",
        "SaddleBrown",
        "Salmon",
        "SandyBrown",
        "SeaGreen",
        "SeaShell",
        "Sienna",
        "Silver",
        "SkyBlue",
        "SlateBlue",
        "SlateGray",
        "Snow",
        "SpringGreen",
        "SteelBlue",
        "Tan",
        "Teal",
        "Thistle",
        "Tomato",
        "Turquoise",
        "Violet",
        "Wheat",
        "White",
        "WhiteSmoke",
        "Yellow",
        "YellowGreen",
    };

    /// <summary>True for <c>#RGB</c>, <c>#ARGB</c>, <c>#RRGGBB</c> and <c>#AARRGGBB</c>.</summary>
    public static bool IsHexColor(string value)
    {
        if (value.Length is not (4 or 5 or 7 or 9) || value[0] != '#')
        {
            return false;
        }

        for (var i = 1; i < value.Length; i++)
        {
            if (!IsHexDigit(value[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>True for a hexadecimal or <c>sc#</c> color; these are colors wherever they appear.</summary>
    public static bool IsColorLiteral(string value) =>
        IsHexColor(value) || value.StartsWith("sc#", StringComparison.OrdinalIgnoreCase);

    /// <summary>True for a named WPF color other than <c>Transparent</c> (case-insensitive, as the XAML parser reads them).</summary>
    public static bool IsNamedColor(string value) => NamedColors.Contains(value);

    /// <summary>True when the member called <paramref name="name"/> is <c>Transparent</c> of <c>Colors</c> or <c>Brushes</c>.</summary>
    public static bool IsTransparent(string name) =>
        string.Equals(name, TransparentName, StringComparison.Ordinal);

    /// <summary>True when a XAML attribute or property element called <paramref name="name"/> takes a color or a brush.</summary>
    public static bool IsColorName(string name) =>
        IdentifierNames.EndsWithAnyWord(name, ColorWords);

    private static bool IsHexDigit(char c) =>
        c is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F');
}
