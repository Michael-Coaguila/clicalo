using ArchUnitNET.Domain;

namespace Clicalo.Architecture.Tests.Support;

/// <summary>
/// A named set of types (an assembly, a namespace subtree, a type or a union of them). Rules are written against
/// zones so the same rule runs on the product assemblies and on the violating fixtures of the negative tests.
/// </summary>
internal sealed class Zone
{
    private readonly Func<IType, bool> _contains;

    private Zone(string name, Func<IType, bool> contains)
    {
        Name = name;
        _contains = contains;
    }

    /// <summary>Human-readable name used in rule descriptions and failure messages.</summary>
    public string Name { get; }

    /// <summary>A zone that contains nothing.</summary>
    public static Zone Nothing { get; } = new("nothing", _ => false);

    /// <summary>Every type compiled into the assembly with this simple name.</summary>
    public static Zone Assembly(string assemblyName) =>
        new(
            assemblyName,
            type => string.Equals(AssemblyNameOf(type), assemblyName, StringComparison.Ordinal)
        );

    /// <summary>
    /// Simple assembly name of <paramref name="type"/>. ArchUnitNET reports the simple name for loaded assemblies and
    /// the full display name (<c>PresentationCore, Version=…</c>) for assemblies that are only referenced.
    /// </summary>
    public static string AssemblyNameOf(IType type)
    {
        var name = type.Assembly?.Name ?? string.Empty;
        var comma = name.IndexOf(',', StringComparison.Ordinal);
        return comma < 0 ? name : name[..comma];
    }

    /// <summary>Every type in <paramref name="ns"/> or in any namespace below it.</summary>
    public static Zone Namespace(string ns) => new(ns, type => IsInNamespace(type, ns));

    /// <summary>The type with this full name and every type nested in it (including compiler-generated ones).</summary>
    public static Zone Type(string fullName) =>
        new(
            fullName,
            type =>
                string.Equals(type.FullName, fullName, StringComparison.Ordinal)
                || type.FullName.StartsWith(fullName + "+", StringComparison.Ordinal)
                || type.FullName.StartsWith(fullName + "/", StringComparison.Ordinal)
        );

    /// <summary>Types that satisfy <paramref name="predicate"/>.</summary>
    public static Zone Where(string name, Func<IType, bool> predicate) => new(name, predicate);

    /// <summary>Types that belong to any of <paramref name="zones"/>.</summary>
    public static Zone AnyOf(string name, params Zone[] zones) =>
        new(name, type => zones.Any(zone => zone.Contains(type)));

    /// <summary>True when <paramref name="type"/> belongs to the zone.</summary>
    public bool Contains(IType type) => _contains(type);

    /// <summary>Types of this zone that are not in <paramref name="other"/>.</summary>
    public Zone Except(Zone other) =>
        new(Name + " except " + other.Name, type => Contains(type) && !other.Contains(type));

    /// <summary>True when the namespace of <paramref name="type"/> is <paramref name="ns"/> or below it.</summary>
    public static bool IsInNamespace(IType type, string ns)
    {
        var name = type.Namespace?.FullName ?? string.Empty;
        return string.Equals(name, ns, StringComparison.Ordinal)
            || name.StartsWith(ns + ".", StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override string ToString() => Name;
}
