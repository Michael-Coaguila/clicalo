using Clicalo.Domain.Execution.Internal;
using Clicalo.Domain.Library;

namespace Clicalo.Domain.Tests.Execution.Support;

/// <summary>
/// The engine's rules for its tests: <see cref="ShortcutCompleteness.Evaluate"/> once the domain package has
/// implemented it, and until then a local double of ATJ-009 with the same meaning (M2-ownership.md: the engine tests
/// run against the contracts and are validated again when rebased on m2/skeleton).
/// </summary>
internal static class TestRules
{
    private static readonly Lazy<EngineRules> Rules = new(static () =>
    {
        try
        {
            _ = ShortcutCompleteness.Evaluate(
                new SystemAction(new Catalog.SystemCommandId("lock"))
            );
            return EngineRules.Default;
        }
        catch (NotImplementedException)
        {
            return new EngineRules(Completeness);
        }
    });

    /// <summary>The rules the tests use.</summary>
    public static EngineRules Engine => Rules.Value;

    private static CompletenessIssue Completeness(ShortcutAction action) =>
        action switch
        {
            TapAction tap when tap.Chord.IsEmpty => CompletenessIssue.MissingKeys,
            HoldAction hold when hold.Chord.IsEmpty => CompletenessIssue.MissingKeys,
            ToggleAction toggle when toggle.Chord.IsEmpty => CompletenessIssue.MissingKeys,
            TextAction { Text.IsAvailable: false } => CompletenessIssue.TextUnavailable,
            TextAction { Text.Length: 0 } => CompletenessIssue.MissingText,
            MacroAction macro when macro.Steps.IsEmpty => CompletenessIssue.MissingSteps,
            UrlAction { Target: UrlTarget.Raw } => CompletenessIssue.InvalidAddress,
            AppAction { Target: AppTarget.Raw } => CompletenessIssue.InvalidApp,
            _ => CompletenessIssue.None,
        };
}
