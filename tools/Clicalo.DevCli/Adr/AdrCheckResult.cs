using System.Collections.Immutable;

namespace Clicalo.DevCli.Adr;

/// <summary>Outcome of <see cref="AdrCheck.Evaluate"/>.</summary>
internal sealed record AdrCheckResult(ImmutableArray<AdrTouch> Touched, bool AdrChanged)
{
    /// <summary>True when no sensitive path changed, or an ADR changed with them.</summary>
    public bool Passed => Touched.IsEmpty || AdrChanged;
}
