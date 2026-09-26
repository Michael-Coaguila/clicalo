namespace Clicalo.Windowing.IntegrationTests.Automation.Rules;

/// <summary>
/// UIA006: notice regions are live regions with the urgency of the view model: the notice and status bars Polite,
/// the panic strip and errors Assertive (ACC-001).
/// </summary>
public sealed class Uia006LiveRegionRule : IUiaRule
{
    /// <inheritdoc />
    public string Id => "UIA006";

    /// <inheritdoc />
    public IEnumerable<UiaViolation> Check(UiaNode root, UiaExpectations expectations)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(expectations);
        foreach (var node in root.DescendantsAndSelf())
        {
            if (expectations.Of(node) is { LiveSetting: { } live } && node.LiveSetting != live)
            {
                yield return new UiaViolation(
                    Id,
                    node.Label,
                    $"has LiveSetting {node.LiveSetting}; the view model says {live}."
                );
            }
        }
    }
}
