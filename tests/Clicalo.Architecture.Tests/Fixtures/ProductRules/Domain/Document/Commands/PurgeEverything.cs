namespace Clicalo.Architecture.Tests.Fixtures.ProductRules.Domain.Document.Commands;

/// <summary>Fixture: a destructive command missing from the closed list.</summary>
public sealed class PurgeEverything : IDestructiveCommand
{
    public int Apply(int document) => document * 0;
}
