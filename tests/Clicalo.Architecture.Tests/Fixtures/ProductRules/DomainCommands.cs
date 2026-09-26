namespace Clicalo.Architecture.Tests.Fixtures.ProductRules.Domain.Document.Commands;

/// <summary>Fixture: a listed destructive command that implements the marker (allowed).</summary>
public sealed class DeleteShortcut : IDestructiveCommand
{
    public int Apply(int document) => document - 1;
}

/// <summary>Fixture: a destructive command missing from the closed list.</summary>
public sealed class PurgeEverything : IDestructiveCommand
{
    public int Apply(int document) => document * 0;
}

/// <summary>Fixture: a listed destructive command that forgot the marker.</summary>
public sealed class DeleteProfile : IDocumentCommand
{
    public int Apply(int document) => document - 2;
}

/// <summary>Fixture: an undo exemption that is a document command (allowed).</summary>
public sealed class RecordUsage : IDocumentCommand
{
    public int Apply(int document) => document;
}

/// <summary>Fixture: an undo exemption that names something that is not a document command.</summary>
public sealed class FinishOnboarding
{
    public bool Done { get; set; }
}
