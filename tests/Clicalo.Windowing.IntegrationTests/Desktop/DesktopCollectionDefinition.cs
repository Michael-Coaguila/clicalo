namespace Clicalo.Windowing.IntegrationTests.Desktop;

/// <summary>
/// Every desktop test of this project runs sequentially and never in parallel with any other test: the foreground,
/// the pointer and the input queue belong to the whole session. Packages add their fixtures (InputProbe session,
/// lab surfaces) as class fixtures, not here, so this file does not change.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DesktopCollectionDefinition
{
    /// <summary>Name to use in <c>[Collection(DesktopCollectionDefinition.Name)]</c>.</summary>
    public const string Name = "Desktop";
}
