using System.Text;

namespace Clicalo.Windowing.IntegrationTests.Automation.Rules;

/// <summary>
/// The UIA rules of blueprint §10.2 that can be checked on a snapshot of the tree: UIA001–UIA006, UIA008 and
/// UIA010. The same verifier runs on in-process peer snapshots (every PR, headless) and on snapshots taken by a UIA
/// client from a real surface (desktop tests).
/// </summary>
public static class UiaVerifier
{
    /// <summary>The rules, in id order.</summary>
    public static IReadOnlyList<IUiaRule> Rules { get; } =
    [
        new Uia001NameRule(),
        new Uia002ControlTypeRule(),
        new Uia003PatternRule(),
        new Uia004StateRule(),
        new Uia005TargetSizeRule(),
        new Uia006LiveRegionRule(),
        new Uia008GlyphNameRule(),
        new Uia010DictationRule(),
    ];

    /// <summary>Every violation of every rule.</summary>
    public static IReadOnlyList<UiaViolation> Verify(UiaNode root, UiaExpectations expectations) =>
        [.. Rules.SelectMany(rule => rule.Check(root, expectations))];

    /// <summary>Fails with every violation, one per line, when the tree breaks a rule.</summary>
    public static void ShouldPass(UiaNode root, UiaExpectations expectations)
    {
        var violations = Verify(root, expectations);
        if (violations.Count == 0)
        {
            return;
        }

        var message = new StringBuilder("The UI Automation tree breaks the UIA rules:");
        foreach (var violation in violations)
        {
            message.AppendLine().Append("  ").Append(violation);
        }

        message.AppendLine().AppendLine("Tree:").Append(UiaTreeText.Format(root));
        throw new Xunit.Sdk.XunitException(message.ToString());
    }
}
