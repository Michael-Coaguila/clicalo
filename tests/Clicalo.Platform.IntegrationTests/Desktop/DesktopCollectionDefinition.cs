namespace Clicalo.Platform.IntegrationTests.Desktop;

/// <summary>
/// Every desktop test shares one InputProbe and runs sequentially, never in parallel with any other test: the
/// foreground and the input queue belong to the whole session.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DesktopCollectionDefinition : ICollectionFixture<DesktopProbeFixture>
{
    /// <summary>Name to use in <c>[Collection(DesktopCollectionDefinition.Name)]</c>.</summary>
    public const string Name = "Desktop";
}
