using Clicalo.Architecture.Tests.Fixtures.ConfinedApis.External.Windows.Win32;

namespace Clicalo.Architecture.Tests.Fixtures.ConfinedApis.Platform.Windows.Foreground;

/// <summary>Fixture: the one type allowed to call SetForegroundWindow.</summary>
public static class ForegroundControl
{
    public static bool TrySetForeground(nint window) => PInvoke.SetForegroundWindow(window);
}
