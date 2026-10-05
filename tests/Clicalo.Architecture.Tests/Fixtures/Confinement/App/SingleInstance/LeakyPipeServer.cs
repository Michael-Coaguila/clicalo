using Clicalo.Architecture.Tests.Fixtures.Confinement.Application.Engine;
using Clicalo.Architecture.Tests.Fixtures.Confinement.Application.Ports;

namespace Clicalo.Architecture.Tests.Fixtures.Confinement.App.SingleInstance;

/// <summary>Fixture: a single-instance pipe server that reaches the engine and the injector, which D13 forbids.</summary>
public sealed class LeakyPipeServer(EngineHost engine, IInputInjector injector)
{
    public void Handle()
    {
        engine.Send();
        injector.Inject("win+r");
    }
}
