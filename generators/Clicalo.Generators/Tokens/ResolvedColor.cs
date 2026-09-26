using Clicalo.Design.Math;

namespace Clicalo.Generators.Tokens;

/// <summary>A token color as it will be rendered (8-bit, gamut-mapped), with its provenance for docs and errors.</summary>
internal sealed class ResolvedColor(Rgba8 value, string source, DataPosition position, string? note)
{
    public Rgba8 Value { get; } = value;

    /// <summary>The value as written in the data (after any documented correction).</summary>
    public string Source { get; } = source;

    public DataPosition Position { get; } = position;

    /// <summary>Provenance worth surfacing in the generated docs: a correction or an alias.</summary>
    public string? Note { get; } = note;
}
