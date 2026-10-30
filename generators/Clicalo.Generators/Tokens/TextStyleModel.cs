namespace Clicalo.Generators.Tokens;

/// <summary>One named text style of <c>design-system.json</c> → <c>typography</c>.</summary>
internal sealed class TextStyleModel(
    string key,
    string family,
    int weight,
    double size,
    double lineHeight,
    double letterSpacing,
    bool uppercase
)
{
    public string Key { get; } = key;

    /// <summary><c>ui</c> (Atkinson Hyperlegible) or <c>keys</c> (JetBrains Mono).</summary>
    public string Family { get; } = family;

    public int Weight { get; } = weight;

    public double Size { get; } = size;

    public double LineHeight { get; } = lineHeight;

    /// <summary>Extra space between letters, in em.</summary>
    public double LetterSpacing { get; } = letterSpacing;

    public bool Uppercase { get; } = uppercase;
}
