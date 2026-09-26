using Clicalo.Architecture.Tests.Fixtures.Layers.Infrastructure;

namespace Clicalo.Architecture.Tests.Fixtures.Layers.Domain;

/// <summary>Fixture: a domain type that reaches Infrastructure inside a method body only.</summary>
public sealed class LeakyAggregate
{
    public int Count { get; private set; }

    public string Save()
    {
        Count++;
        return new Repository().Name;
    }
}

/// <summary>Fixture: a domain type that depends only on itself and the BCL.</summary>
public sealed class CleanValue
{
    public int Value { get; } = 42;

    public int Twice() => Value * 2;
}
