namespace Clicalo.Architecture.Tests.Fixtures.ProductRules.Domain.Document.Commands;

/// <summary>Fixture: a listed destructive command that forgot the marker.</summary>
public sealed class DeleteProfile : IDocumentCommand
{
    public int Apply(int document) => document - 2;
}
