using System.Collections.Immutable;
using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;

namespace Clicalo.Architecture.Tests.Support;

/// <summary>
/// The capability modules of one layer (blueprint §4.3): a module is <c>{root}.{Module}</c> and every namespace
/// below it. A module may use itself and every module it reaches through <c>dependsOn</c>.
/// </summary>
internal sealed class ModuleMatrix
{
    private readonly ImmutableSortedDictionary<string, ImmutableArray<string>> _dependsOn;
    private readonly ImmutableArray<string> _longestFirst;

    public ModuleMatrix(
        string rootNamespace,
        IEnumerable<(string Name, IEnumerable<string> DependsOn)> modules
    )
    {
        RootNamespace = rootNamespace;
        var list = modules.ToList();
        Errors = Validate(list);
        _dependsOn = list.GroupBy(m => m.Name, StringComparer.Ordinal)
            .ToImmutableSortedDictionary(
                g => g.Key,
                g =>
                    g.SelectMany(m => m.DependsOn)
                        .Distinct(StringComparer.Ordinal)
                        .ToImmutableArray(),
                StringComparer.Ordinal
            );
        _longestFirst = [.. _dependsOn.Keys.OrderByDescending(name => name.Length)];
        Reachable = _dependsOn.Keys.ToImmutableSortedDictionary(
            name => name,
            name => ReachableFrom(name),
            StringComparer.Ordinal
        );
    }

    public string RootNamespace { get; }

    /// <summary>Structural errors: duplicates, unknown or self dependencies, cycles. Empty when valid.</summary>
    public ImmutableArray<string> Errors { get; }

    /// <summary>For each module, the modules it may use (transitive closure of <c>dependsOn</c>).</summary>
    public ImmutableSortedDictionary<string, ImmutableSortedSet<string>> Reachable { get; }

    /// <summary>Builds the matrix of <c>architecture/domain-modules.json</c>.</summary>
    public static ModuleMatrix From(DomainModules document) =>
        new(
            document.RootNamespace,
            document.Modules.Select(m => (m.Name, (IEnumerable<string>)m.DependsOn))
        );

    /// <summary>The module of <paramref name="type"/>, or null when it is in no declared module.</summary>
    public string? ModuleOf(IType type) =>
        _longestFirst.FirstOrDefault(module =>
            Zone.IsInNamespace(type, RootNamespace + "." + module)
        );

    /// <summary>True when code of module <paramref name="from"/> may use module <paramref name="to"/>.</summary>
    public bool Allows(string from, string to) =>
        string.Equals(from, to, StringComparison.Ordinal)
        || (Reachable.TryGetValue(from, out var reachable) && reachable.Contains(to));

    /// <summary>
    /// The rule over the types of <paramref name="layer"/>: every type under the root namespace belongs to a declared
    /// module and only uses modules its module may use. Types outside the root namespace are compiler
    /// infrastructure (embedded attributes) and are ignored.
    /// </summary>
    public IArchRule Rule(Zone layer, string because)
    {
        var root = Zone.Namespace(RootNamespace);
        return Support.Rule.For(
            layer,
            "belong to a declared module of "
                + RootNamespace
                + " and only use the modules it may use",
            because,
            type => Violations(type, root)
        );
    }

    private IEnumerable<string> Violations(IType type, Zone root)
    {
        if (!root.Contains(type))
        {
            yield break;
        }

        var module = ModuleOf(type);
        if (module is null)
        {
            yield return "is in no declared module (" + type.Namespace?.FullName + ")";
            yield break;
        }

        foreach (var target in Support.Rule.DependencyTargets(type).Where(root.Contains))
        {
            var targetModule = ModuleOf(target);
            if (targetModule is not null && !Allows(module, targetModule))
            {
                yield return module + " -> " + targetModule + " (uses " + target.FullName + ")";
            }
        }
    }

    private ImmutableSortedSet<string> ReachableFrom(string module)
    {
        var seen = new SortedSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>(_dependsOn[module]);
        while (pending.TryPop(out var next))
        {
            if (seen.Add(next) && _dependsOn.TryGetValue(next, out var further))
            {
                foreach (var dependency in further)
                {
                    pending.Push(dependency);
                }
            }
        }

        return [.. seen];
    }

    private static ImmutableArray<string> Validate(
        List<(string Name, IEnumerable<string> DependsOn)> modules
    )
    {
        var errors = new List<string>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (name, _) in modules.Where(m => !names.Add(m.Name)))
        {
            errors.Add("Module " + name + " is declared twice.");
        }

        var edges = modules
            .GroupBy(m => m.Name, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.SelectMany(m => m.DependsOn).ToList(),
                StringComparer.Ordinal
            );
        foreach (var (name, dependsOn) in edges)
        {
            errors.AddRange(
                dependsOn
                    .Where(d => !edges.ContainsKey(d))
                    .Select(d => "Module " + name + " depends on the undeclared module " + d + ".")
            );
            if (dependsOn.Contains(name, StringComparer.Ordinal))
            {
                errors.Add("Module " + name + " depends on itself.");
            }
        }

        errors.AddRange(Cycles(edges));
        return [.. errors];
    }

    private static List<string> Cycles(Dictionary<string, List<string>> edges)
    {
        // Depth-first search with colors: gray nodes are on the current path, so reaching one closes a cycle.
        var state = new Dictionary<string, int>(StringComparer.Ordinal);
        var path = new List<string>();
        var cycles = new List<string>();

        void Visit(string node)
        {
            state[node] = 1;
            path.Add(node);
            foreach (var next in edges[node].Where(edges.ContainsKey))
            {
                var color = state.GetValueOrDefault(next);
                if (color == 1)
                {
                    var cycle = path.Skip(path.IndexOf(next)).Append(next);
                    cycles.Add("Cycle: " + string.Join(" -> ", cycle) + ".");
                }
                else if (color == 0)
                {
                    Visit(next);
                }
            }

            path.RemoveAt(path.Count - 1);
            state[node] = 2;
        }

        foreach (
            var node in edges.Keys.Order(StringComparer.Ordinal).Where(n => !state.ContainsKey(n))
        )
        {
            Visit(node);
        }

        return cycles;
    }
}
