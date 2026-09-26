using System.Globalization;

namespace Clicalo.Windowing.IntegrationTests.Automation.Rules;

/// <summary>
/// UIA005: every enabled, on-screen actionable element is at least 44 × 44 logical pixels times the monitor scale
/// (REG-02, ACC-002), half a physical pixel of rounding allowed.
/// </summary>
public sealed class Uia005TargetSizeRule : IUiaRule
{
    private const double Rounding = 0.5;

    /// <inheritdoc />
    public string Id => "UIA005";

    /// <inheritdoc />
    public IEnumerable<UiaViolation> Check(UiaNode root, UiaExpectations expectations)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(expectations);
        foreach (
            var node in root.DescendantsAndSelf()
                .Where(node => node.IsActionable && node.IsEnabled && !node.IsOffscreen)
        )
        {
            var minimum = expectations.MinimumTargetSize * node.Scale;
            if (node.Bounds.IsEmpty)
            {
                yield return new UiaViolation(Id, node.Label, "has no bounding rectangle.");
            }
            else if (
                node.Bounds.Width + Rounding < minimum
                || node.Bounds.Height + Rounding < minimum
            )
            {
                yield return new UiaViolation(
                    Id,
                    node.Label,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"measures {node.Bounds.Width:0.#} × {node.Bounds.Height:0.#} px; the touch target is {minimum:0.#} × {minimum:0.#} px at scale {node.Scale:0.##}."
                    )
                );
            }
        }
    }
}
