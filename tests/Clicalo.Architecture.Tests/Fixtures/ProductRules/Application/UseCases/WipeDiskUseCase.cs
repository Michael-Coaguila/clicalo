namespace Clicalo.Architecture.Tests.Fixtures.ProductRules.Application.UseCases;

/// <summary>Fixture: a destructive use case missing from the closed list.</summary>
[Destructive]
public sealed class WipeDiskUseCase
{
    public bool Done { get; set; }
}
