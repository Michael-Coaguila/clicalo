namespace Clicalo.Windowing.IntegrationTests.Automation.Rules;

/// <summary>
/// One of the UIA rules of blueprint §10.2 that can be checked on a snapshot of the tree. UIA007 (gesture-free
/// equivalents) belongs to Presentation and UIA009 (invoking never changes the foreground) is behavioral: see
/// <see cref="ForegroundInvariant"/>.
/// </summary>
public interface IUiaRule
{
    /// <summary>UIA001…UIA010.</summary>
    string Id { get; }

    /// <summary>The violations of the rule in <paramref name="root"/> and its descendants.</summary>
    IEnumerable<UiaViolation> Check(UiaNode root, UiaExpectations expectations);
}
