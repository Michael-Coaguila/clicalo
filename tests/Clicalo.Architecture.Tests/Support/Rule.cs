using ArchUnitNET.Domain;
using ArchUnitNET.Domain.Dependencies;
using ArchUnitNET.Fluent;
using ArchUnitNET.Fluent.Conditions;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Clicalo.Architecture.Tests.Support;

/// <summary>Builds ArchUnitNET rules whose condition reports every violation of a type in one message.</summary>
internal static class Rule
{
    /// <summary>
    /// A rule over the types of <paramref name="subject"/>: a type passes when <paramref name="violationsOf"/>
    /// returns nothing. Rules never require positive results, because many zones stay empty until their module
    /// lands (M2–M4) and an empty zone is not a defect. What keeps an empty result honest: ArchitectureLoadingTests
    /// proves every product assembly is loaded, and each rule has a negative test that fails it on a fixture.
    /// </summary>
    public static IArchRule For(
        Zone subject,
        string should,
        string because,
        Func<IType, IEnumerable<string>> violationsOf
    ) =>
        Types()
            .That()
            .FollowCustomPredicate(subject.Contains, "reside in " + subject.Name)
            .Should()
            .FollowCustomCondition(type => Evaluate(type, violationsOf), should)
            .Because(because)
            .WithoutRequiringPositiveResults();

    /// <summary>Every type <paramref name="type"/> depends on (signatures, bodies, attributes, generics).</summary>
    public static IEnumerable<IType> DependencyTargets(IType type) =>
        type.Dependencies.Select(dependency => dependency.Target).Distinct();

    /// <summary>Every method or constructor that <paramref name="type"/> calls.</summary>
    public static IEnumerable<IMember> CalledMembers(IType type) =>
        type
            .Dependencies.OfType<MethodCallDependency>()
            .Select(call => call.TargetMember)
            .Distinct();

    /// <summary>
    /// One line per failing type (its name and every violation), without the rule description, so negative tests
    /// can assert on what was reported and on what was not.
    /// </summary>
    public static string Violations(IArchRule rule, LoadedArchitecture architecture) =>
        string.Join(
            Environment.NewLine,
            rule.Evaluate(architecture)
                .Where(result => !result.Passed)
                .Select(result => result.Description)
        );

    private static ConditionResult Evaluate(
        IType type,
        Func<IType, IEnumerable<string>> violationsOf
    )
    {
        var violations = violationsOf(type)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
        return new ConditionResult(type, violations.Count == 0, string.Join("; ", violations));
    }
}
