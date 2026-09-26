using Clicalo.Architecture.Tests.Fixtures.Confinement.Application.Engine;
using Clicalo.Architecture.Tests.Fixtures.Confinement.Application.Ports;

namespace Clicalo.Architecture.Tests.Fixtures.Confinement.Application.Ipc;

/// <summary>Fixture: an IPC server that reaches the engine, which invariant D13 forbids.</summary>
public sealed class LeakyIpcServer(EngineHost engine)
{
    public void Handle() => engine.Send();
}

/// <summary>Fixture: an IPC handler that only uses IShellNavigator.</summary>
public sealed class ShowRequestHandler(IShellNavigator navigator)
{
    public void Handle() => navigator.Show();
}
