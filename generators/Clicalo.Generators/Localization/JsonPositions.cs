using System.Collections.Generic;

namespace Clicalo.Generators.Localization;

/// <summary>
/// Refines the positions reported by <c>MiniJson</c> (which marks where a value starts) so that i18n errors point
/// at the exact key or at the exact character inside a text. Lines and columns are 1-based and count UTF-16 code
/// units, like <c>MiniJson</c> and Roslyn.
/// </summary>
internal sealed class JsonPositions
{
    private const char ByteOrderMark = (char)0xFEFF;

    private readonly string _text;
    private readonly List<int> _lineStarts = [];

    public JsonPositions(string text)
    {
        _text = text;
        // MiniJson skips a leading BOM without counting it as a column.
        _lineStarts.Add(text.Length > 0 && text[0] == ByteOrderMark ? 1 : 0);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                _lineStarts.Add(i + 1);
            }
        }
    }

    /// <summary>
    /// Position of the opening quote of the property name whose value starts at <paramref name="valueLine"/> and
    /// <paramref name="valueColumn"/>. Falls back to the value position when the text is not shaped as expected.
    /// </summary>
    public (int Line, int Column) KeyOf(int valueLine, int valueColumn)
    {
        var offset = OffsetOf(valueLine, valueColumn);
        if (offset < 0)
        {
            return (valueLine, valueColumn);
        }

        var p = SkipWhitespaceBackwards(offset - 1);
        if (p < 0 || _text[p] != ':')
        {
            return (valueLine, valueColumn);
        }

        p = SkipWhitespaceBackwards(p - 1);
        if (p < 0 || _text[p] != '"')
        {
            return (valueLine, valueColumn);
        }

        for (var q = p - 1; q >= 0; q--)
        {
            if (_text[q] == '"' && !IsEscaped(q))
            {
                return PositionOf(q);
            }
        }

        return (valueLine, valueColumn);
    }

    /// <summary>
    /// Position of the character at <paramref name="decodedIndex"/> of the decoded string whose opening quote is at
    /// <paramref name="valueLine"/> and <paramref name="valueColumn"/>, accounting for escape sequences.
    /// </summary>
    public (int Line, int Column) CharInString(int valueLine, int valueColumn, int decodedIndex)
    {
        var quote = OffsetOf(valueLine, valueColumn);
        if (quote < 0 || _text[quote] != '"')
        {
            return (valueLine, valueColumn);
        }

        var p = quote + 1;
        for (var i = 0; i < decodedIndex && p < _text.Length; i++)
        {
            p +=
                _text[p] != '\\' ? 1
                : p + 1 < _text.Length && _text[p + 1] == 'u' ? 6
                : 2;
        }

        return PositionOf(p);
    }

    private int OffsetOf(int line, int column)
    {
        if (line < 1 || line > _lineStarts.Count || column < 1)
        {
            return -1;
        }

        var offset = _lineStarts[line - 1] + column - 1;
        return offset < _text.Length ? offset : -1;
    }

    private (int Line, int Column) PositionOf(int offset)
    {
        var line = _lineStarts.BinarySearch(offset);
        if (line < 0)
        {
            line = System.Math.Max(0, ~line - 1);
        }

        return (line + 1, offset - _lineStarts[line] + 1);
    }

    private int SkipWhitespaceBackwards(int p)
    {
        while (p >= 0 && _text[p] is ' ' or '\t' or '\r' or '\n')
        {
            p--;
        }

        return p;
    }

    private bool IsEscaped(int quoteOffset)
    {
        var backslashes = 0;
        for (var i = quoteOffset - 1; i >= 0 && _text[i] == '\\'; i--)
        {
            backslashes++;
        }

        return backslashes % 2 == 1;
    }
}
