namespace Clicalo.Design.Math;

/// <summary>How a <see cref="CssColor"/> was written in the data.</summary>
public enum CssColorNotation
{
    /// <summary><c>oklch(L C H)</c> or <c>oklch(L C H / A)</c>.</summary>
    Oklch,

    /// <summary><c>#RRGGBB</c>.</summary>
    Hex,
}
