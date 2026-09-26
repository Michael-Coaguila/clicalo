using Clicalo.Architecture.Tests.Fixtures.Confinement.Application.Ports;

namespace Clicalo.Architecture.Tests.Fixtures.Confinement.Application.Engine;

/// <summary>Fixture: the engine, allowed to use the injector.</summary>
public sealed class EngineHost(IInputInjector injector)
{
    public void Send() => injector.Inject("ctrl+c");
}
