namespace Clicalo.Architecture.Tests.Fixtures.Layers.Domain;

/// <summary>Fixture: a domain type that depends only on itself and the BCL.</summary>
public sealed class CleanValue
{
    public int Value { get; } = 42;

    public int Twice() => Value * 2;
}
