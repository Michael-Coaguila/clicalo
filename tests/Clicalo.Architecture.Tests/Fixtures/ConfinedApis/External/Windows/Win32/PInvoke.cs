namespace Clicalo.Architecture.Tests.Fixtures.ConfinedApis.External.Windows.Win32;

/// <summary>Fixture: stands for the CsWin32-generated Windows.Win32.PInvoke class.</summary>
public static class PInvoke
{
    public static bool SetForegroundWindow(nint window) => window != 0;

    public static bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach) =>
        attach && idAttach != idAttachTo;

    public static uint SendInput(uint count) => count;
}
