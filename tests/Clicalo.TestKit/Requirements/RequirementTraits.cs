using System.Reflection;

namespace Clicalo.TestKit.Requirements;

/// <summary>
/// The traceability convention: a test that verifies a requirement of the catalog carries
/// <c>[Trait("Req", "&lt;ID&gt;")]</c>, once per requirement (on the method, or on the class for all its tests).
/// Tests that need an interactive desktop also carry <c>[Trait("Requires", "Desktop")]</c>.
/// </summary>
/// <remarks>
/// Traits are read through <see cref="CustomAttributeData"/> by attribute name, so this library does not depend on
/// a test framework. Every test project can assert that its references exist with <see cref="FindUnknown"/>.
/// </remarks>
public static class RequirementTraits
{
    /// <summary>Trait name that links a test to a requirement identifier.</summary>
    public const string Req = "Req";

    /// <summary>Trait name for an environment prerequisite, such as <see cref="Desktop"/>.</summary>
    public const string Requires = "Requires";

    /// <summary>Value of <see cref="Requires"/> for tests that need an interactive desktop session.</summary>
    public const string Desktop = "Desktop";

    private const string TraitAttributeName = "Xunit.TraitAttribute";

    private const BindingFlags DeclaredMembers =
        BindingFlags.Public
        | BindingFlags.NonPublic
        | BindingFlags.Instance
        | BindingFlags.Static
        | BindingFlags.DeclaredOnly;

    /// <summary>Every <c>[Trait("Req", …)]</c> declared by the types of <paramref name="assembly"/>.</summary>
    public static IReadOnlyList<RequirementReference> FindIn(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        var references = new List<RequirementReference>();
        foreach (var type in assembly.GetTypes())
        {
            Collect(type, type.FullName ?? type.Name, references);
            foreach (var method in type.GetMethods(DeclaredMembers))
            {
                Collect(method, (type.FullName ?? type.Name) + "." + method.Name, references);
            }
        }

        return references;
    }

    /// <summary>The references of <paramref name="assembly"/> whose identifier is not in <paramref name="catalog"/>.</summary>
    public static IReadOnlyList<RequirementReference> FindUnknown(
        Assembly assembly,
        RequirementCatalog catalog
    )
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return [.. FindIn(assembly).Where(reference => !catalog.Contains(reference.Id))];
    }

    private static void Collect(
        MemberInfo member,
        string test,
        List<RequirementReference> references
    )
    {
        foreach (var attribute in member.GetCustomAttributesData())
        {
            if (
                string.Equals(
                    attribute.AttributeType.FullName,
                    TraitAttributeName,
                    StringComparison.Ordinal
                )
                && attribute.ConstructorArguments
                    is [{ Value: string name }, { Value: string value }]
                && string.Equals(name, Req, StringComparison.Ordinal)
            )
            {
                references.Add(new RequirementReference(value, test));
            }
        }
    }
}
