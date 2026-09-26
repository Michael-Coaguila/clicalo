using System.Buffers;

namespace Clicalo.Architecture.Tests.Fixtures.Confinement.Domain.Library;

/// <summary>Fixture: a secret that is only revealed through a callback.</summary>
public sealed class SecretText(string value)
{
    public int Length => value.Length;

    public void WithRevealed<TState>(TState state, ReadOnlySpanAction<char, TState> use) =>
        use(value, state);
}
