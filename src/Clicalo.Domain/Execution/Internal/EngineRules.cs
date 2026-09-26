using Clicalo.Domain.Library;

namespace Clicalo.Domain.Execution.Internal;

/// <summary>
/// The rules of other modules the engine applies. The product always uses <see cref="Default"/>; the engine's own
/// tests pass a stand-in only while a rule of the domain package is not integrated yet (M2 builds both at once,
/// docs/testing/spikes/M2-ownership.md).
/// </summary>
/// <param name="Completeness">The completeness rule of ATJ-009 (<see cref="ShortcutCompleteness.Evaluate"/>).</param>
internal sealed record EngineRules(Func<ShortcutAction, CompletenessIssue> Completeness)
{
    /// <summary>The rules of the product.</summary>
    public static EngineRules Default { get; } = new(ShortcutCompleteness.Evaluate);
}
