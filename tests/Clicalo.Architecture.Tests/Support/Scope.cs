namespace Clicalo.Architecture.Tests.Support;

/// <summary>
/// Where a rule applies: the product, or a fixture area that mirrors product names under
/// <c>Fixtures.{Area}</c> (for example <c>Clicalo.Application.Engine</c> becomes
/// <c>Clicalo.Architecture.Tests.Fixtures.{Area}.Application.Engine</c>). Rules written against a scope run
/// unchanged on the product and on the violating fixtures of the negative tests.
/// </summary>
internal sealed class Scope
{
    private const string ProductRoot = "Clicalo";

    private readonly Func<string, string> _map;

    private Scope(Func<string, string> map, Zone universe)
    {
        _map = map;
        Universe = universe;
    }

    /// <summary>The product assemblies, without the generated interop code.</summary>
    public static Scope Product { get; } = new(name => name, Support.Product.Code);

    /// <summary>The types the rules of this scope inspect.</summary>
    public Zone Universe { get; }

    /// <summary>The fixture area <c>Fixtures.{area}</c>, which mirrors the product names.</summary>
    public static Scope Fixture(string area)
    {
        var root = FixtureArchitecture.Name(area);
        return new(
            name =>
                name.StartsWith(ProductRoot + ".", StringComparison.Ordinal)
                    ? root + name[ProductRoot.Length..]
                    : root + ".External." + name,
            Zone.Namespace(root)
        );
    }

    /// <summary>The mapped name of a product namespace or type.</summary>
    public string Name(string productName) => _map(productName);

    /// <summary>The namespace <paramref name="productNamespace"/> (mapped) and everything below it.</summary>
    public Zone Namespace(string productNamespace) => Zone.Namespace(_map(productNamespace));

    /// <summary>The type <paramref name="productType"/> (mapped) and its nested types.</summary>
    public Zone Type(string productType) => Zone.Type(_map(productType));

    /// <summary>A namespace or a type, whichever <paramref name="productName"/> names.</summary>
    public Zone NamespaceOrType(string productName) =>
        Zone.AnyOf(_map(productName), Namespace(productName), Type(productName));
}
