using Clicalo.Architecture.Tests.Fixtures.Confinement.Application.Ports;

namespace Clicalo.Architecture.Tests.Fixtures.Confinement.Application.Ipc;

/// <summary>Fixture: an IPC handler that only uses IShellNavigator.</summary>
public sealed class ShowRequestHandler(IShellNavigator navigator)
{
    public void Handle() => navigator.Show();
}
