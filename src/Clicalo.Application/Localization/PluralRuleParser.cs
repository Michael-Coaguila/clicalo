using System.Collections.Immutable;
using System.Globalization;

namespace Clicalo.Application.Localization;

/// <summary>
/// Parser of the CLDR plural condition syntax (UTS #35, part 3, «Plural rules syntax»):
/// <code>
/// condition     = and_condition ('or' and_condition)*
/// and_condition = relation ('and' relation)*
/// relation      = operand ('%' value)? ('=' | '!=') range_list
/// operand       = 'n' | 'i' | 'v' | 'w' | 'f' | 't' | 'c' | 'e'
/// range_list    = (value | value '..' value) (',' range_list)*
/// </code>
/// Samples (<c>@integer …</c>, <c>@decimal …</c>) are allowed and ignored. The legacy keywords
/// (<c>is</c>, <c>in</c>, <c>within</c>, <c>mod</c>, <c>not</c>) are not: CLDR data no longer uses them.
/// </summary>
internal sealed class PluralRuleParser
{
    private readonly string _text;
    private readonly int _end;
    private int _pos;

    private PluralRuleParser(string text)
    {
        _text = text;
        var samples = text.IndexOf('@', StringComparison.Ordinal);
        _end = samples < 0 ? text.Length : samples;
    }

    /// <summary>Parses a condition into its OR of ANDs of relations.</summary>
    /// <exception cref="FormatException">The condition is not valid; the message says where.</exception>
    public static ImmutableArray<ImmutableArray<PluralRelation>> Parse(string text)
    {
        var parser = new PluralRuleParser(text);
        var alternatives = ImmutableArray.CreateBuilder<ImmutableArray<PluralRelation>>();
        do
        {
            alternatives.Add(parser.ParseAnd());
        } while (parser.TryKeyword("or"));

        parser.SkipWhitespace();
        if (parser._pos < parser._end)
        {
            throw parser.Error("expected 'and', 'or' or the end of the rule");
        }

        return alternatives.ToImmutable();
    }

    private ImmutableArray<PluralRelation> ParseAnd()
    {
        var relations = ImmutableArray.CreateBuilder<PluralRelation>();
        do
        {
            relations.Add(ParseRelation());
        } while (TryKeyword("and"));

        return relations.ToImmutable();
    }

    private PluralRelation ParseRelation()
    {
        SkipWhitespace();
        if (
            _pos >= _end
            || "niwvftce".IndexOf(_text[_pos], StringComparison.Ordinal) < 0
            || IsLetterAt(_pos + 1)
        )
        {
            throw Error("expected an operand (n, i, v, w, f, t, c or e)");
        }

        var operand = _text[_pos++];
        decimal? modulus = null;
        if (TrySymbol("%"))
        {
            modulus = ReadValue();
            if (modulus == 0m)
            {
                throw Error("the modulus must not be 0");
            }
        }

        bool negated;
        if (TrySymbol("!="))
        {
            negated = true;
        }
        else if (TrySymbol("="))
        {
            negated = false;
        }
        else
        {
            throw Error("expected '=' or '!='");
        }

        var ranges = ImmutableArray.CreateBuilder<PluralRange>();
        do
        {
            var low = ReadValue();
            var high = TrySymbol("..") ? ReadValue() : low;
            if (high < low)
            {
                throw Error("a range must go from the lower to the higher value");
            }

            ranges.Add(new PluralRange(low, high));
        } while (TrySymbol(","));

        return new PluralRelation(operand, modulus, negated, ranges.ToImmutable());
    }

    private decimal ReadValue()
    {
        SkipWhitespace();
        var start = _pos;
        while (_pos < _end && char.IsAsciiDigit(_text[_pos]))
        {
            _pos++;
        }

        if (start == _pos)
        {
            throw Error("expected a whole number");
        }

        return decimal.Parse(
            _text.AsSpan(start, _pos - start),
            NumberStyles.None,
            CultureInfo.InvariantCulture
        );
    }

    private bool TrySymbol(string symbol)
    {
        SkipWhitespace();
        if (
            _pos + symbol.Length > _end
            || string.CompareOrdinal(_text, _pos, symbol, 0, symbol.Length) != 0
        )
        {
            return false;
        }

        _pos += symbol.Length;
        return true;
    }

    private bool TryKeyword(string keyword)
    {
        SkipWhitespace();
        if (
            _pos + keyword.Length > _end
            || string.CompareOrdinal(_text, _pos, keyword, 0, keyword.Length) != 0
            || IsLetterAt(_pos + keyword.Length)
        )
        {
            return false;
        }

        _pos += keyword.Length;
        return true;
    }

    private bool IsLetterAt(int index) => index < _end && char.IsAsciiLetter(_text[index]);

    private void SkipWhitespace()
    {
        while (_pos < _end && char.IsWhiteSpace(_text[_pos]))
        {
            _pos++;
        }
    }

    private FormatException Error(string reason) =>
        new(
            string.Create(
                CultureInfo.InvariantCulture,
                $"Invalid CLDR plural rule \"{_text}\" at position {_pos + 1}: {reason}."
            )
        );
}
