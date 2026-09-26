namespace Clicalo.Data.Tests.Tokens;

/// <summary>One entry of <c>contrast-pairs.json</c>.</summary>
internal sealed record PairSpec(
    bool IsText,
    string Foreground,
    IReadOnlyList<string> Backgrounds,
    string Use
);
