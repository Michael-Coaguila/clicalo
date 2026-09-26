using System.Numerics;
using FlaUI.Core.Definitions;

namespace Clicalo.Windowing.IntegrationTests.Automation.Rules;

/// <summary>
/// UIA003: expected elements support exactly the patterns of their action (ACC-001), and every button supports
/// exactly one of Invoke, Toggle and ExpandCollapse, so «clic» has one meaning.
/// </summary>
public sealed class Uia003PatternRule : IUiaRule
{
    private const UiaPatterns Checked = UiaPatterns.Actionable | UiaPatterns.Value;

    /// <inheritdoc />
    public string Id => "UIA003";

    /// <inheritdoc />
    public IEnumerable<UiaViolation> Check(UiaNode root, UiaExpectations expectations)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(expectations);
        foreach (var node in root.DescendantsAndSelf())
        {
            var actual = node.Patterns & Checked;
            if (expectations.Of(node) is { } expected && actual != (expected.Patterns & Checked))
            {
                yield return new UiaViolation(
                    Id,
                    node.Label,
                    $"supports {actual}; the view model says {expected.Patterns & Checked}."
                );
            }
            else if (
                node.ControlType == ControlType.Button
                && BitOperations.PopCount((uint)(node.Patterns & UiaPatterns.ButtonActions)) != 1
            )
            {
                yield return new UiaViolation(
                    Id,
                    node.Label,
                    $"is a button with {node.Patterns & UiaPatterns.ButtonActions}; it needs exactly one of Invoke, Toggle and ExpandCollapse."
                );
            }
        }
    }
}
