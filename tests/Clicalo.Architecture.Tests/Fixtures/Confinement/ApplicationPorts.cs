namespace Clicalo.Architecture.Tests.Fixtures.Confinement.Application.Ports;

/// <summary>Fixture port: the confined input injector.</summary>
public interface IInputInjector
{
    void Inject(string keys);
}

/// <summary>Fixture port: the confined foreground control.</summary>
public interface IForegroundControl
{
    bool TrySetForeground(nint window);
}

/// <summary>Fixture port: the only port the IPC server may use.</summary>
public interface IShellNavigator
{
    void Show();
}
