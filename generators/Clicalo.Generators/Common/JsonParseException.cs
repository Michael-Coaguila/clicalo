using System;

namespace Clicalo.Generators.Common;

/// <summary>Error raised when a JSON document is malformed, with its 1-based position.</summary>
internal sealed class JsonParseException : Exception
{
    public JsonParseException(string message, int line, int column)
        : base(message)
    {
        Line = line;
        Column = column;
    }

    public JsonParseException() { }

    public JsonParseException(string message)
        : base(message) { }

    public JsonParseException(string message, Exception innerException)
        : base(message, innerException) { }

    public int Line { get; }

    public int Column { get; }
}
