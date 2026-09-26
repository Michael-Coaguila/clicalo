namespace Clicalo.Generators.Common;

/// <summary>
/// Which slice of generated code a consuming project wants. Set with the MSBuild property
/// <c>ClicaloGeneratorProfile</c> (Domain, UiWpf) and exposed through <c>CompilerVisibleProperty</c>.
/// </summary>
internal enum GeneratorProfile
{
    None,
    Domain,
    UiWpf,
}
