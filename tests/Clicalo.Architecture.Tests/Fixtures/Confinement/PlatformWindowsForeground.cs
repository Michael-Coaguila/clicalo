using Clicalo.Architecture.Tests.Fixtures.Confinement.Application.Ports;

namespace Clicalo.Architecture.Tests.Fixtures.Confinement.Platform.Windows.Foreground;

/// <summary>Fixture: the adapter of the foreground port.</summary>
public sealed class ForegroundControl : IForegroundControl
{
    public bool TrySetForeground(nint window) => window != 0;
}
