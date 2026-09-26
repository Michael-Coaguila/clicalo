namespace Clicalo.Architecture.Tests.Fixtures.ProductRules.Domain.Document.Commands;

/// <summary>Fixture: an undo exemption that is a document command (allowed).</summary>
public sealed class RecordUsage : IDocumentCommand
{
    public int Apply(int document) => document;
}
