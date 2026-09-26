namespace Clicalo.Windowing.IntegrationTests.Automation.Rules;

/// <summary>
/// UIA008: no glyph is used as a name (ACC-001: ←, ↑, ⌫ and ★ have names that can be said). A name, without its
/// voice number, must contain a letter or a digit and no icon-font character (Unicode private use area).
/// </summary>
public sealed class Uia008GlyphNameRule : IUiaRule
{
    /// <inheritdoc />
    public string Id => "UIA008";

    /// <inheritdoc />
    public IEnumerable<UiaViolation> Check(UiaNode root, UiaExpectations expectations)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(expectations);
        foreach (var node in root.DescendantsAndSelf().Where(node => node.Name.Length > 0))
        {
            var spoken = VoiceNames.Strip(node.Name);
            if (!spoken.Any(char.IsLetterOrDigit))
            {
                yield return new UiaViolation(
                    Id,
                    node.Label,
                    $"is named «{node.Name}», which cannot be said."
                );
            }
            else if (spoken.Any(IsPrivateUse))
            {
                yield return new UiaViolation(
                    Id,
                    node.Label,
                    $"has an icon glyph in its name «{node.Name}»."
                );
            }
        }
    }

    private static bool IsPrivateUse(char character) =>
        character is >= (char)0xE000 and <= (char)0xF8FF;
}
