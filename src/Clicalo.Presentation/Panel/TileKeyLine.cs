namespace Clicalo.Presentation.Panel;

/// <summary>The line under the name of a tile (CUA-007) and its spoken form (CUA-008).</summary>
/// <param name="Line">What the tile shows: the combination, abbreviated in size S.</param>
/// <param name="Spoken">The same with full key names, for screen readers.</param>
public sealed record TileKeyLine(string Line, string Spoken)
{
    /// <summary>No line: an action without a combination, or «Mostrar teclas» off.</summary>
    public static TileKeyLine None { get; } = new(string.Empty, string.Empty);
}
