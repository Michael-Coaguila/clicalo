namespace Clicalo.Architecture.Tests.Fixtures.ProductRules.Application.UseCases;

/// <summary>Fixture: the marker of destructive use cases.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class DestructiveAttribute : Attribute;

/// <summary>Fixture: a listed destructive use case with its marker (allowed).</summary>
[Destructive]
public sealed class UninstallSystemComponentUseCase
{
    public bool Done { get; set; }
}

/// <summary>Fixture: a listed destructive use case without its marker.</summary>
public sealed class RollbackVersionUseCase
{
    public bool Done { get; set; }
}

/// <summary>Fixture: a destructive use case missing from the closed list.</summary>
[Destructive]
public sealed class WipeDiskUseCase
{
    public bool Done { get; set; }
}
