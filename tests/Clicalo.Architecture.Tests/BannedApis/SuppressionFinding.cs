namespace Clicalo.Architecture.Tests.BannedApis;

/// <summary>An RS0030 suppression found in a source file.</summary>
internal sealed record SuppressionFinding(string File, int Line, SuppressionKind Kind)
{
    public override string ToString() => File + "(" + Line + "): " + Kind + " RS0030 suppression";
}
