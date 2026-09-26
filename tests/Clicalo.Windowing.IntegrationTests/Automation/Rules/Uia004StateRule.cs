namespace Clicalo.Windowing.IntegrationTests.Automation.Rules;

/// <summary>
/// UIA004: the states UI Automation reports match the view model: toggle state (three states for sticky keys),
/// expand and collapse state, the key combination as help text and the state text as item status (ACC-003).
/// </summary>
public sealed class Uia004StateRule : IUiaRule
{
    /// <inheritdoc />
    public string Id => "UIA004";

    /// <inheritdoc />
    public IEnumerable<UiaViolation> Check(UiaNode root, UiaExpectations expectations)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(expectations);
        foreach (var node in root.DescendantsAndSelf())
        {
            if (node.Patterns.HasFlag(UiaPatterns.Toggle) && node.ToggleState is null)
            {
                yield return new UiaViolation(Id, node.Label, "supports Toggle without a state.");
            }

            if (
                node.Patterns.HasFlag(UiaPatterns.ExpandCollapse)
                && node.ExpandCollapseState is null
            )
            {
                yield return new UiaViolation(
                    Id,
                    node.Label,
                    "supports ExpandCollapse without a state."
                );
            }

            if (expectations.Of(node) is not { } expected)
            {
                continue;
            }

            if (expected.ToggleState is { } toggle && node.ToggleState != toggle)
            {
                yield return new UiaViolation(
                    Id,
                    node.Label,
                    $"reports ToggleState {node.ToggleState}; the view model says {toggle}."
                );
            }

            if (expected.ExpandCollapseState is { } expand && node.ExpandCollapseState != expand)
            {
                yield return new UiaViolation(
                    Id,
                    node.Label,
                    $"reports ExpandCollapseState {node.ExpandCollapseState}; the view model says {expand}."
                );
            }

            if (
                expected.HelpText is { } help
                && !string.Equals(node.HelpText, help, StringComparison.Ordinal)
            )
            {
                yield return new UiaViolation(
                    Id,
                    node.Label,
                    $"has help text «{node.HelpText}»; the view model says «{help}»."
                );
            }

            if (
                expected.ItemStatus is { } status
                && !string.Equals(node.ItemStatus, status, StringComparison.Ordinal)
            )
            {
                yield return new UiaViolation(
                    Id,
                    node.Label,
                    $"has item status «{node.ItemStatus}»; the view model says «{status}»."
                );
            }
        }
    }
}
