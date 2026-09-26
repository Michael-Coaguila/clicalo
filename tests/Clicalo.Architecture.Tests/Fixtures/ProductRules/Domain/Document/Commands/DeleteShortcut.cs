namespace Clicalo.Architecture.Tests.Fixtures.ProductRules.Domain.Document.Commands;

/// <summary>Fixture: a listed destructive command that implements the marker (allowed).</summary>
public sealed class DeleteShortcut : IDestructiveCommand
{
    public int Apply(int document) => document - 1;
}
