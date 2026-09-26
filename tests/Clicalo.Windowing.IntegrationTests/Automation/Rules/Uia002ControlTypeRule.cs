using FlaUI.Core.Definitions;

namespace Clicalo.Windowing.IntegrationTests.Automation.Rules;

/// <summary>
/// UIA002: expected elements have the control type of the view model, and no actionable element hides behind a
/// generic role (Text, Pane, Group, Custom, Image) that voice tools do not offer as clickable.
/// </summary>
public sealed class Uia002ControlTypeRule : IUiaRule
{
    private static readonly HashSet<ControlType> GenericRoles =
    [
        ControlType.Text,
        ControlType.Pane,
        ControlType.Group,
        ControlType.Custom,
        ControlType.Image,
    ];

    /// <inheritdoc />
    public string Id => "UIA002";

    /// <inheritdoc />
    public IEnumerable<UiaViolation> Check(UiaNode root, UiaExpectations expectations)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(expectations);
        foreach (var node in root.DescendantsAndSelf())
        {
            if (expectations.Of(node) is { } expected && node.ControlType != expected.ControlType)
            {
                yield return new UiaViolation(
                    Id,
                    node.Label,
                    $"is a {node.ControlType}; the view model says {expected.ControlType}."
                );
            }
            else if (node.IsActionable && GenericRoles.Contains(node.ControlType))
            {
                yield return new UiaViolation(
                    Id,
                    node.Label,
                    $"is actionable but exposed as {node.ControlType}."
                );
            }
        }
    }
}
