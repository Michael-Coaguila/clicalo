namespace Clicalo.Architecture.Tests;

/// <summary>Shares one <see cref="EnforcementBuild"/> between the slow build tests.</summary>
[CollectionDefinition(Name)]
public sealed class SharedEnforcementBuild : ICollectionFixture<EnforcementBuild>
{
    public const string Name = "Enforcement build";
}
