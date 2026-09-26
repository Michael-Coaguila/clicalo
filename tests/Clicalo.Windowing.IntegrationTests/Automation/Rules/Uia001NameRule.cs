namespace Clicalo.Windowing.IntegrationTests.Automation.Rules;

/// <summary>
/// UIA001: every actionable element has a non-empty name, every expected element is in the tree, and its name is
/// the localized name of the view model preceded by «{n} » exactly when «Numbers for voice» is on (ACC-009,
/// REG-06).
/// </summary>
public sealed class Uia001NameRule : IUiaRule
{
    /// <inheritdoc />
    public string Id => "UIA001";

    /// <inheritdoc />
    public IEnumerable<UiaViolation> Check(UiaNode root, UiaExpectations expectations)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(expectations);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in root.DescendantsAndSelf())
        {
            var expected = expectations.Of(node);
            if (expected is not null)
            {
                seen.Add(expected.AutomationId);
            }

            if ((node.IsActionable || expected is not null) && string.IsNullOrWhiteSpace(node.Name))
            {
                yield return new UiaViolation(Id, node.Label, "has no name.");
            }
            else if (
                expected is not null
                && !string.Equals(node.Name, expected.FullName, StringComparison.Ordinal)
            )
            {
                yield return new UiaViolation(
                    Id,
                    node.Label,
                    $"is named «{node.Name}»; the view model says «{expected.FullName}»."
                );
            }
        }

        foreach (var missing in expectations.Elements.Keys.Where(id => !seen.Contains(id)))
        {
            yield return new UiaViolation(Id, missing, "is not in the UI Automation control view.");
        }
    }
}
