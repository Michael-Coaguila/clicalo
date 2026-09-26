namespace Clicalo.Generators.Tokens;

/// <summary>Where a value was written: file path and 1-based line and column.</summary>
internal sealed class DataPosition(string path, int line, int column)
{
    public string Path { get; } = path;

    public int Line { get; } = line;

    public int Column { get; } = column;

    /// <summary>The position <paramref name="offset"/> characters after this one, on the same line.</summary>
    public DataPosition Shift(int offset) => new(Path, Line, Column + offset);
}
