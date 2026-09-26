namespace Clicalo.Architecture.Tests.Fixtures.Confinement.Application.Ports;

/// <summary>Fixture port: the confined foreground control.</summary>
public interface IForegroundControl
{
    bool TrySetForeground(nint window);
}
