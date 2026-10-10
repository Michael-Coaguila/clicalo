namespace Clicalo.Architecture.Tests.Fixtures.ProductRules.Application.UseCases;

/// <summary>Fixture: a listed destructive use case with its marker (allowed).</summary>
[Destructive]
public sealed class UninstallKeepOrDeleteDataUseCase
{
    public bool Done { get; set; }
}
