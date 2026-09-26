using Clicalo.Architecture.Tests.Fixtures.Confinement.Application.Foreground;

namespace Clicalo.Architecture.Tests.Fixtures.Confinement.Infrastructure.Rogue;

/// <summary>Fixture: an adapter implementing an Application interface that is not in Application.Ports.</summary>
public sealed class MisplacedAdapter : IMisplacedPort
{
    public int Runs { get; private set; }

    public void Run() => Runs++;
}
