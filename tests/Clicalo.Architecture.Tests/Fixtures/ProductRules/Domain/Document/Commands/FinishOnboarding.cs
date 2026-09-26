namespace Clicalo.Architecture.Tests.Fixtures.ProductRules.Domain.Document.Commands;

/// <summary>Fixture: an undo exemption that names something that is not a document command.</summary>
public sealed class FinishOnboarding
{
    public bool Done { get; set; }
}
