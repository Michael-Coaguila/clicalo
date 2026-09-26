using ArchUnitNET.Loader;

namespace Clicalo.Architecture.Tests.Support;

/// <summary>
/// The architecture of this test assembly, where the negative tests keep types that violate each rule on purpose.
/// They live under <see cref="Root"/>, which the product rules never see because they only load src/ assemblies.
/// </summary>
internal static class FixtureArchitecture
{
    /// <summary>Root namespace of the violating fixtures.</summary>
    public const string Root = "Clicalo.Architecture.Tests.Fixtures";

    private static readonly Lazy<LoadedArchitecture> LazyArchitecture = new(() =>
        new ArchLoader().LoadAssemblies(typeof(FixtureArchitecture).Assembly).Build()
    );

    /// <summary>This test assembly, loaded with ArchUnitNET.</summary>
    public static LoadedArchitecture Architecture => LazyArchitecture.Value;

    /// <summary>The fixture namespace <c>Fixtures.{relative}</c> and everything below it.</summary>
    public static Zone Namespace(string relative) => Zone.Namespace(Root + "." + relative);

    /// <summary>Full name of a fixture type or namespace.</summary>
    public static string Name(string relative) => Root + "." + relative;
}
