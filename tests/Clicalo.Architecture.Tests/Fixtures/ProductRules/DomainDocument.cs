namespace Clicalo.Architecture.Tests.Fixtures.ProductRules.Domain.Document;

/// <summary>Fixture: the document command contract.</summary>
public interface IDocumentCommand
{
    int Apply(int document);
}

/// <summary>Fixture: the destructive command marker.</summary>
public interface IDestructiveCommand : IDocumentCommand;
