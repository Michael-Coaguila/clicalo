using FlaUI.Core.Definitions;

namespace Clicalo.Windowing.IntegrationTests.Automation.Rules;

/// <summary>
/// UIA010: every free text field (an Edit with the Value pattern) has a sibling button with Invoke named «Dictar»
/// (or «Pegar» in the AI key field), in the localized names of <see cref="UiaExpectations.DictationNames"/>
/// (ACC-011, REG-05).
/// </summary>
public sealed class Uia010DictationRule : IUiaRule
{
    /// <inheritdoc />
    public string Id => "UIA010";

    /// <inheritdoc />
    public IEnumerable<UiaViolation> Check(UiaNode root, UiaExpectations expectations)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(expectations);
        foreach (var (parent, field) in root.Edges())
        {
            if (field.ControlType != ControlType.Edit || !field.Patterns.HasFlag(UiaPatterns.Value))
            {
                continue;
            }

            var hasDictation = parent.Children.Any(sibling =>
                !ReferenceEquals(sibling, field)
                && sibling.ControlType == ControlType.Button
                && sibling.Patterns.HasFlag(UiaPatterns.Invoke)
                && expectations.DictationNames.Contains(
                    VoiceNames.Strip(sibling.Name),
                    StringComparer.Ordinal
                )
            );
            if (!hasDictation)
            {
                yield return new UiaViolation(
                    Id,
                    field.Label,
                    "is a free text field without a sibling dictation button."
                );
            }
        }
    }
}
