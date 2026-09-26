using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;

namespace Clicalo.Architecture.Tests.Support;

/// <summary>Reusable rule shapes of blueprint §4.4, mechanism 2, parameterized by zones.</summary>
internal static class DependencyRules
{
    private const string InternalSegment = ".Internal";

    /// <summary>
    /// Types of <paramref name="subject"/> may depend on types of <paramref name="universe"/> only when they are in
    /// <paramref name="subject"/> itself or in one of <paramref name="allowed"/>. Types outside the universe (BCL,
    /// packages) are left to other rules.
    /// </summary>
    public static IArchRule OnlyDependOn(
        Zone subject,
        Zone universe,
        IReadOnlyCollection<Zone> allowed,
        string because
    )
    {
        var description =
            allowed.Count == 0 ? "nothing else" : string.Join(", ", allowed.Select(z => z.Name));
        return Rule.For(
            subject,
            "only depend on " + description + " within " + universe.Name,
            because,
            type =>
                Rule.DependencyTargets(type)
                    .Where(target => universe.Contains(target) && !subject.Contains(target))
                    .Where(target => !allowed.Any(zone => zone.Contains(target)))
                    .Select(target => "depends on " + target.FullName)
        );
    }

    /// <summary>Types of <paramref name="subject"/> must not depend on any type of <paramref name="forbidden"/>.</summary>
    public static IArchRule NotDependOn(Zone subject, Zone forbidden, string because) =>
        Rule.For(
            subject,
            "not depend on " + forbidden.Name,
            because,
            type =>
                Rule.DependencyTargets(type)
                    .Where(forbidden.Contains)
                    .Select(target => "depends on " + target.FullName)
        );

    /// <summary>
    /// Only <paramref name="allowed"/> (and <paramref name="target"/> itself) may depend on <paramref name="target"/>
    /// among the types of <paramref name="universe"/>.
    /// </summary>
    public static IArchRule OnlyAllowedDependOn(
        Zone target,
        Zone allowed,
        Zone universe,
        string because
    ) => NotDependOn(universe.Except(allowed).Except(target), target, because);

    /// <summary>
    /// Among the types of <paramref name="universe"/>, only those in <paramref name="allowed"/> may call a member
    /// matched by <paramref name="member"/>.
    /// </summary>
    public static IArchRule OnlyAllowedCall(
        Func<IMember, bool> member,
        string memberDescription,
        Zone allowed,
        Zone universe,
        string because
    ) =>
        Rule.For(
            universe.Except(allowed),
            "not call " + memberDescription,
            because,
            type =>
                Rule.CalledMembers(type).Where(member).Select(called => "calls " + called.FullName)
        );

    /// <summary>
    /// No type of <paramref name="universe"/> uses the <c>.Internal</c> namespace of another module: the types in
    /// <c>X.Internal</c> (and below) are private to the module <c>X</c> (blueprint §4.3).
    /// </summary>
    public static IArchRule NotUseOtherModulesInternals(Zone universe, string because) =>
        Rule.For(
            universe,
            "not use the .Internal namespace of another module",
            because,
            type =>
                Rule.DependencyTargets(type)
                    .Where(universe.Contains)
                    .Select(target => (target, owner: InternalOwner(target)))
                    .Where(pair => pair.owner is not null && !IsWithin(type, pair.owner))
                    .Select(pair => "uses " + pair.target.FullName + ", internal to " + pair.owner)
        );

    /// <summary>
    /// Types of <paramref name="adapters"/> may implement interfaces of <paramref name="application"/> only when they
    /// live in the ports namespace <paramref name="portsNamespace"/> (blueprint §4.4: ports live in Application.Ports).
    /// </summary>
    public static IArchRule ImplementOnlyPorts(
        Zone adapters,
        Zone application,
        string portsNamespace,
        string because
    ) =>
        Rule.For(
            adapters,
            "implement only interfaces of " + application.Name + " that live in " + portsNamespace,
            because,
            type =>
                type.ImplementedInterfaces.Where(application.Contains)
                    .Where(port => !Zone.IsInNamespace(port, portsNamespace))
                    .Select(port =>
                        "implements " + port.FullName + ", which is not in " + portsNamespace
                    )
        );

    private static string? InternalOwner(IType type)
    {
        var ns = type.Namespace?.FullName ?? string.Empty;
        var index = (ns + ".").IndexOf(InternalSegment + ".", StringComparison.Ordinal);
        return index < 0 ? null : ns[..index];
    }

    private static bool IsWithin(IType type, string ns) => Zone.IsInNamespace(type, ns);
}
