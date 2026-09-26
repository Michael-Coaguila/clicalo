using System;
using System.Globalization;

namespace Clicalo.Design.Math;

/// <summary>
/// Strict parser for the CSS subset used by the token data (<c>data/tokens/*.json</c>, after the design
/// package's <c>theme-palettes.json</c>): <c>oklch(L C H)</c>, <c>oklch(L C H / A)</c>, <c>#RRGGBB</c> and
/// the border shorthand <c>&lt;n&gt;px solid &lt;color&gt;</c>. Anything else is rejected with the offset of
/// the first offending character, so the generator can point at the exact line and column.
/// </summary>
public static class CssParser
{
    private const double MaxHue = 360d;

    /// <summary>Parses a color. On failure <paramref name="error"/> says why and where.</summary>
    public static bool TryParseColor(string text, out CssColor color, out CssSyntaxError error)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        var reader = new Reader(text);
        reader.SkipWhitespace();
        if (!TryReadColor(reader, out color, out error))
        {
            return false;
        }

        reader.SkipWhitespace();
        if (!reader.AtEnd)
        {
            color = default;
            error = reader.Error("Unexpected text after the color.");
            return false;
        }

        return true;
    }

    /// <summary>Parses <c>&lt;n&gt;px solid &lt;color&gt;</c>. On failure <paramref name="error"/> says why and where.</summary>
    public static bool TryParseBorder(string text, out CssBorder border, out CssSyntaxError error)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        border = default;
        var reader = new Reader(text);
        reader.SkipWhitespace();
        var widthStart = reader.Position;
        if (!reader.TryReadNumber(out var width))
        {
            error = reader.Error(
                "Expected the border width in px, for example '1px solid #ffffff'."
            );
            return false;
        }

        if (!reader.TryConsume("px"))
        {
            error = reader.Error("Expected the unit 'px' after the border width.");
            return false;
        }

        if (width <= 0d)
        {
            error = new CssSyntaxError(widthStart, "The border width must be greater than zero.");
            return false;
        }

        if (!reader.RequireWhitespace(out error))
        {
            return false;
        }

        if (!reader.TryConsume("solid"))
        {
            error = reader.Error("Only the 'solid' border style is supported.");
            return false;
        }

        if (!reader.RequireWhitespace(out error))
        {
            return false;
        }

        if (!TryReadColor(reader, out var color, out error))
        {
            return false;
        }

        reader.SkipWhitespace();
        if (!reader.AtEnd)
        {
            error = reader.Error("Unexpected text after the border color.");
            return false;
        }

        border = new CssBorder(width, color);
        return true;
    }

    private static bool TryReadColor(Reader reader, out CssColor color, out CssSyntaxError error)
    {
        color = default;
        if (reader.Peek() == '#')
        {
            return TryReadHex(reader, out color, out error);
        }

        if (reader.TryConsume("oklch("))
        {
            return TryReadOklchArguments(reader, out color, out error);
        }

        error = reader.Error("Expected 'oklch(L C H)', 'oklch(L C H / A)' or '#RRGGBB'.");
        return false;
    }

    private static bool TryReadHex(Reader reader, out CssColor color, out CssSyntaxError error)
    {
        color = default;
        var start = reader.Position;
        reader.Advance(); // '#'
        var digits = new byte[3];
        for (var i = 0; i < digits.Length; i++)
        {
            if (!reader.TryReadHexByte(out digits[i]))
            {
                error = reader.Error("Expected exactly six hexadecimal digits after '#'.");
                return false;
            }
        }

        if (Reader.IsHexDigit(reader.Peek()))
        {
            error = new CssSyntaxError(
                start,
                "Only '#RRGGBB' is supported; use oklch(L C H / A) for transparency."
            );
            return false;
        }

        color = CssColor.FromHex(new Rgba8(digits[0], digits[1], digits[2]));
        error = default;
        return true;
    }

    private static bool TryReadOklchArguments(
        Reader reader,
        out CssColor color,
        out CssSyntaxError error
    )
    {
        color = default;
        reader.SkipWhitespace();
        if (
            !reader.TryReadComponent("lightness", 0d, 1d, out var lightness, out error)
            || !reader.RequireWhitespace(out error)
            || !reader.TryReadComponent("chroma", 0d, double.MaxValue, out var chroma, out error)
            || !reader.RequireWhitespace(out error)
            || !reader.TryReadComponent("hue", 0d, MaxHue, out var hue, out error)
        )
        {
            return false;
        }

        reader.SkipWhitespace();
        var alpha = 1d;
        if (reader.Peek() == '/')
        {
            reader.Advance();
            reader.SkipWhitespace();
            if (!reader.TryReadComponent("alpha", 0d, 1d, out alpha, out error))
            {
                return false;
            }

            reader.SkipWhitespace();
        }

        if (reader.Peek() != ')')
        {
            error = reader.Error("Expected ')' (or '/ alpha') after the hue.");
            return false;
        }

        reader.Advance();
        color = CssColor.FromOklch(new Oklch(lightness, chroma, hue, alpha));
        error = default;
        return true;
    }

    private sealed class Reader(string text)
    {
        private const char None = '\0';

        public int Position { get; private set; }

        public bool AtEnd => Position >= text.Length;

        public static bool IsHexDigit(char c) =>
            c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';

        public char Peek() => AtEnd ? None : text[Position];

        public void Advance() => Position++;

        public CssSyntaxError Error(string message) => new(Position, message);

        public void SkipWhitespace()
        {
            while (!AtEnd && IsWhitespace(text[Position]))
            {
                Position++;
            }
        }

        public bool RequireWhitespace(out CssSyntaxError error)
        {
            if (AtEnd || !IsWhitespace(text[Position]))
            {
                error = Error("Expected whitespace between components.");
                return false;
            }

            SkipWhitespace();
            error = default;
            return true;
        }

        /// <summary>Consumes <paramref name="literal"/> case-insensitively (CSS keywords and functions are).</summary>
        public bool TryConsume(string literal)
        {
            if (Position + literal.Length > text.Length)
            {
                return false;
            }

            if (
                string.Compare(
                    text,
                    Position,
                    literal,
                    0,
                    literal.Length,
                    StringComparison.OrdinalIgnoreCase
                ) != 0
            )
            {
                return false;
            }

            Position += literal.Length;
            return true;
        }

        public bool TryReadHexByte(out byte value)
        {
            value = 0;
            if (
                Position + 2 > text.Length
                || !IsHexDigit(text[Position])
                || !IsHexDigit(text[Position + 1])
            )
            {
                return false;
            }

            value = byte.Parse(
                text.Substring(Position, 2),
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture
            );
            Position += 2;
            return true;
        }

        /// <summary>CSS &lt;number&gt; without exponent: <c>[+-]? (digits ('.' digits)? | '.' digits)</c>.</summary>
        public bool TryReadNumber(out double value)
        {
            value = 0d;
            var start = Position;
            var pos = Position;
            if (pos < text.Length && (text[pos] == '+' || text[pos] == '-'))
            {
                pos++;
            }

            var integerDigits = CountDigits(pos);
            pos += integerDigits;
            var fractionDigits = 0;
            if (pos < text.Length && text[pos] == '.')
            {
                fractionDigits = CountDigits(pos + 1);
                if (fractionDigits == 0)
                {
                    return false;
                }

                pos += 1 + fractionDigits;
            }

            if (integerDigits == 0 && fractionDigits == 0)
            {
                return false;
            }

            value = double.Parse(
                text.Substring(start, pos - start),
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture
            );
            Position = pos;
            return true;
        }

        public bool TryReadComponent(
            string name,
            double min,
            double max,
            out double value,
            out CssSyntaxError error
        )
        {
            var start = Position;
            if (!TryReadNumber(out value))
            {
                error = Error("Expected the " + name + " as a plain number.");
                return false;
            }

            var next = Peek();
            if (next == '%' || char.IsLetter(next))
            {
                error = Error(
                    "Units and percentages are not supported; write the "
                        + name
                        + " as a plain number."
                );
                return false;
            }

            if (value < min || value > max)
            {
                var range =
                    max == double.MaxValue
                        ? string.Format(CultureInfo.InvariantCulture, "at least {0}", min)
                        : string.Format(
                            CultureInfo.InvariantCulture,
                            "between {0} and {1}",
                            min,
                            max
                        );
                error = new CssSyntaxError(start, "The " + name + " must be " + range + ".");
                return false;
            }

            error = default;
            return true;
        }

        private static bool IsWhitespace(char c) => c is ' ' or '\t' or '\n' or '\r' or '\f';

        private int CountDigits(int from)
        {
            var count = 0;
            while (from + count < text.Length && text[from + count] is >= '0' and <= '9')
            {
                count++;
            }

            return count;
        }
    }
}
