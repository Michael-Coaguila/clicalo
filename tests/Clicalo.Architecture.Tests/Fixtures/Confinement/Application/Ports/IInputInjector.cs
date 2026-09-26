namespace Clicalo.Architecture.Tests.Fixtures.Confinement.Application.Ports;

/// <summary>Fixture port: the confined input injector.</summary>
public interface IInputInjector
{
    void Inject(string keys);
}
