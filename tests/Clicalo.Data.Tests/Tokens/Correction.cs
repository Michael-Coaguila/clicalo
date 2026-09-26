namespace Clicalo.Data.Tests.Tokens;

/// <summary>A documented contrast correction of <c>extra-tokens.json</c> (TEM-004).</summary>
internal sealed record Correction(
    string Theme,
    string Token,
    string From,
    string To,
    string Requirement,
    string Reason
)
{
    public override string ToString() => $"{Theme}.{Token}: {From} → {To}";
}
