using System.Collections.Immutable;
using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;

namespace Clicalo.Architecture.Tests.Support;

/// <summary>One confined API: the members and types it covers and where it may be used.</summary>
internal sealed record ConfinedApi(
    string Name,
    Func<IMember, bool> Member,
    ImmutableArray<string> Types,
    Zone Allowed,
    string Because
)
{
    /// <summary>The rule over <paramref name="universe"/>: outside <see cref="Allowed"/>, no call and no type use.</summary>
    public IArchRule Rule(Zone universe)
    {
        var types = Zone.AnyOf(
            string.Join(", ", Types),
            [.. Types.Select(name => Zone.AnyOf(name, Zone.Namespace(name), Zone.Type(name)))]
        );
        return Support.Rule.For(
            universe.Except(Allowed),
            "not use " + Name + " outside " + Allowed.Name,
            Because,
            type =>
                Support
                    .Rule.CalledMembers(type)
                    .Where(Member)
                    .Select(member => "calls " + member.FullName)
                    .Concat(
                        Support
                            .Rule.DependencyTargets(type)
                            .Where(types.Contains)
                            .Select(target => "uses " + target.FullName)
                    )
        );
    }

    /// <inheritdoc />
    public override string ToString() => Name;
}
