using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Clicalo.Generators.Common;

/// <summary>Strict RFC 8259 parser. Duplicate object keys are rejected because they hide data mistakes.</summary>
internal static class MiniJson
{
    private const int MaxDepth = 64;

    public static JsonNode Parse(string text)
    {
        var reader = new Reader(text);
        reader.SkipWhitespace();
        var root = reader.ReadValue(0);
        reader.SkipWhitespace();
        if (!reader.AtEnd)
        {
            throw reader.Error("Unexpected content after the root value.");
        }

        return root;
    }

    private sealed class Reader
    {
        private readonly string _text;
        private int _pos;
        private int _line = 1;
        private int _lineStart;

        public Reader(string text)
        {
            // Tolerate a UTF-8 BOM decoded as U+FEFF.
            _text = text;
            if (_text.Length > 0 && _text[0] == '﻿')
            {
                _pos = 1;
                _lineStart = 1;
            }
        }

        public bool AtEnd => _pos >= _text.Length;

        private int Column => _pos - _lineStart + 1;

        public JsonParseException Error(string message) => new(message, _line, Column);

        public void SkipWhitespace()
        {
            while (!AtEnd)
            {
                var c = _text[_pos];
                if (c == '\n')
                {
                    _pos++;
                    _line++;
                    _lineStart = _pos;
                }
                else if (c is ' ' or '\t' or '\r')
                {
                    _pos++;
                }
                else
                {
                    return;
                }
            }
        }

        public JsonNode ReadValue(int depth)
        {
            if (depth > MaxDepth)
            {
                throw Error("Maximum nesting depth exceeded.");
            }

            if (AtEnd)
            {
                throw Error("Unexpected end of input.");
            }

            var line = _line;
            var column = Column;
            var c = _text[_pos];
            switch (c)
            {
                case '{':
                    return ReadObject(depth, line, column);
                case '[':
                    return ReadArray(depth, line, column);
                case '"':
                    return JsonNode.String(ReadString(), line, column);
                case 't':
                    Expect("true");
                    return JsonNode.Bool(true, line, column);
                case 'f':
                    Expect("false");
                    return JsonNode.Bool(false, line, column);
                case 'n':
                    Expect("null");
                    return JsonNode.Null(line, column);
                default:
                    if (c == '-' || (c >= '0' && c <= '9'))
                    {
                        return ReadNumber(line, column);
                    }

                    throw Error($"Unexpected character '{c}'.");
            }
        }

        private JsonNode ReadObject(int depth, int line, int column)
        {
            _pos++; // {
            var members = new List<KeyValuePair<string, JsonNode>>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            SkipWhitespace();
            if (!AtEnd && _text[_pos] == '}')
            {
                _pos++;
                return JsonNode.Object(members, line, column);
            }

            while (true)
            {
                SkipWhitespace();
                if (AtEnd || _text[_pos] != '"')
                {
                    throw Error("Expected a property name.");
                }

                var keyLine = _line;
                var keyColumn = Column;
                var key = ReadString();
                if (!seen.Add(key))
                {
                    throw new JsonParseException(
                        $"Duplicate property '{key}'.",
                        keyLine,
                        keyColumn
                    );
                }

                SkipWhitespace();
                if (AtEnd || _text[_pos] != ':')
                {
                    throw Error("Expected ':'.");
                }

                _pos++;
                SkipWhitespace();
                members.Add(new KeyValuePair<string, JsonNode>(key, ReadValue(depth + 1)));
                SkipWhitespace();
                if (AtEnd)
                {
                    throw Error("Unterminated object.");
                }

                var c = _text[_pos++];
                if (c == '}')
                {
                    return JsonNode.Object(members, line, column);
                }

                if (c != ',')
                {
                    throw Error("Expected ',' or '}'.");
                }
            }
        }

        private JsonNode ReadArray(int depth, int line, int column)
        {
            _pos++; // [
            var items = new List<JsonNode>();
            SkipWhitespace();
            if (!AtEnd && _text[_pos] == ']')
            {
                _pos++;
                return JsonNode.Array(items, line, column);
            }

            while (true)
            {
                SkipWhitespace();
                items.Add(ReadValue(depth + 1));
                SkipWhitespace();
                if (AtEnd)
                {
                    throw Error("Unterminated array.");
                }

                var c = _text[_pos++];
                if (c == ']')
                {
                    return JsonNode.Array(items, line, column);
                }

                if (c != ',')
                {
                    throw Error("Expected ',' or ']'.");
                }
            }
        }

        private string ReadString()
        {
            _pos++; // opening quote
            var sb = new StringBuilder();
            while (true)
            {
                if (AtEnd)
                {
                    throw Error("Unterminated string.");
                }

                var c = _text[_pos++];
                if (c == '"')
                {
                    return sb.ToString();
                }

                if (c < 0x20)
                {
                    throw Error("Control characters must be escaped in strings.");
                }

                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }

                if (AtEnd)
                {
                    throw Error("Unterminated escape sequence.");
                }

                var e = _text[_pos++];
                switch (e)
                {
                    case '"':
                        sb.Append('"');
                        break;
                    case '\\':
                        sb.Append('\\');
                        break;
                    case '/':
                        sb.Append('/');
                        break;
                    case 'b':
                        sb.Append('\b');
                        break;
                    case 'f':
                        sb.Append('\f');
                        break;
                    case 'n':
                        sb.Append('\n');
                        break;
                    case 'r':
                        sb.Append('\r');
                        break;
                    case 't':
                        sb.Append('\t');
                        break;
                    case 'u':
                        if (_pos + 4 > _text.Length)
                        {
                            throw Error("Incomplete \\u escape.");
                        }

                        var hex = _text.Substring(_pos, 4);
                        if (
                            !int.TryParse(
                                hex,
                                NumberStyles.AllowHexSpecifier,
                                CultureInfo.InvariantCulture,
                                out var code
                            )
                        )
                        {
                            throw Error("Invalid \\u escape.");
                        }

                        sb.Append((char)code);
                        _pos += 4;
                        break;
                    default:
                        throw Error($"Invalid escape '\\{e}'.");
                }
            }
        }

        private JsonNode ReadNumber(int line, int column)
        {
            var start = _pos;
            if (_text[_pos] == '-')
            {
                _pos++;
            }

            if (AtEnd)
            {
                throw Error("Invalid number.");
            }

            if (_text[_pos] == '0')
            {
                _pos++;
            }
            else if (IsDigit())
            {
                SkipDigits();
            }
            else
            {
                throw Error("Invalid number.");
            }

            if (!AtEnd && _text[_pos] == '.')
            {
                _pos++;
                if (!IsDigit())
                {
                    throw Error("Expected digits after the decimal point.");
                }

                SkipDigits();
            }

            if (!AtEnd && (_text[_pos] == 'e' || _text[_pos] == 'E'))
            {
                _pos++;
                if (!AtEnd && (_text[_pos] == '+' || _text[_pos] == '-'))
                {
                    _pos++;
                }

                if (!IsDigit())
                {
                    throw Error("Expected digits in the exponent.");
                }

                SkipDigits();
            }

            var lexeme = _text.Substring(start, _pos - start);
            var value = double.Parse(lexeme, NumberStyles.Float, CultureInfo.InvariantCulture);
            return JsonNode.Number(value, lexeme, line, column);
        }

        private bool IsDigit() => !AtEnd && _text[_pos] >= '0' && _text[_pos] <= '9';

        private void SkipDigits()
        {
            while (IsDigit())
            {
                _pos++;
            }
        }

        private void Expect(string literal)
        {
            if (string.CompareOrdinal(_text, _pos, literal, 0, literal.Length) != 0)
            {
                throw Error($"Expected '{literal}'.");
            }

            _pos += literal.Length;
        }
    }
}
