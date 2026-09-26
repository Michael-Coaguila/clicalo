using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Clicalo.Analyzers.Presentation;

/// <summary>
/// Maps nodes of a parsed XAML file back to exact source locations, so a diagnostic underlines the attribute value or
/// the text itself and not just the start of the element.
/// </summary>
internal sealed class XamlSource
{
    private const string CDataStart = "<![CDATA[";
    private const string CDataEnd = "]]>";

    private readonly string _path;
    private readonly SourceText _text;

    public XamlSource(string path, SourceText text)
    {
        _path = path;
        _text = text;
    }

    /// <summary>
    /// The span of the attribute value between its quotes or, when <paramref name="part"/> is given and written
    /// verbatim inside it (a <c>FallbackValue</c> within a binding, a literal after its <c>{}</c> escape), of that part.
    /// </summary>
    public Location ValueOf(XAttribute attribute, string? part = null)
    {
        var start = OffsetOf(attribute);
        if (start < 0)
        {
            return FileStart();
        }

        var equals = IndexOf('=', start);
        var quote = equals < 0 ? -1 : SkipWhitespace(equals + 1);
        if (quote < 0 || quote >= _text.Length || _text[quote] is not ('"' or '\''))
        {
            return At(start, start);
        }

        var valueStart = quote + 1;
        var valueEnd = IndexOf(_text[quote], valueStart);
        if (valueEnd < 0)
        {
            return At(start, start);
        }

        var partStart = string.IsNullOrEmpty(part) ? -1 : IndexOf(part!, valueStart, valueEnd);
        return partStart < 0 ? At(valueStart, valueEnd) : At(partStart, partStart + part!.Length);
    }

    /// <summary>The span of a text node without its surrounding whitespace (or the content of a CDATA section).</summary>
    public Location ContentOf(XText node)
    {
        var start = OffsetOf(node);
        if (start < 0)
        {
            return FileStart();
        }

        int end;
        if (node is XCData)
        {
            // The reader reports a CDATA section either at "<![CDATA[" or at its first character.
            if (StartsWith(start, CDataStart))
            {
                start += CDataStart.Length;
            }

            end = IndexOf(CDataEnd, start, _text.Length);
        }
        else
        {
            end = IndexOf('<', start);
        }

        if (end < 0)
        {
            end = _text.Length;
        }

        while (start < end && char.IsWhiteSpace(_text[start]))
        {
            start++;
        }

        while (end > start && char.IsWhiteSpace(_text[end - 1]))
        {
            end--;
        }

        return At(start, end);
    }

    private int OffsetOf(XObject node)
    {
        if (node is not IXmlLineInfo info || !info.HasLineInfo())
        {
            return -1;
        }

        var lineIndex = info.LineNumber - 1;
        if (lineIndex < 0 || lineIndex >= _text.Lines.Count)
        {
            return -1;
        }

        var line = _text.Lines[lineIndex];
        var offset = line.Start + info.LinePosition - 1;
        return offset >= line.Start && offset <= line.End ? offset : -1;
    }

    private int SkipWhitespace(int index)
    {
        while (index < _text.Length && char.IsWhiteSpace(_text[index]))
        {
            index++;
        }

        return index;
    }

    private int IndexOf(char value, int start)
    {
        for (var i = start; i < _text.Length; i++)
        {
            if (_text[i] == value)
            {
                return i;
            }
        }

        return -1;
    }

    private int IndexOf(string value, int start, int end)
    {
        for (var i = start; i + value.Length <= end; i++)
        {
            if (StartsWith(i, value))
            {
                return i;
            }
        }

        return -1;
    }

    private bool StartsWith(int start, string value)
    {
        if (start + value.Length > _text.Length)
        {
            return false;
        }

        for (var i = 0; i < value.Length; i++)
        {
            if (_text[start + i] != value[i])
            {
                return false;
            }
        }

        return true;
    }

    private Location At(int start, int end)
    {
        var span = TextSpan.FromBounds(start, end);
        return Location.Create(_path, span, _text.Lines.GetLinePositionSpan(span));
    }

    private Location FileStart() => At(0, 0);
}
