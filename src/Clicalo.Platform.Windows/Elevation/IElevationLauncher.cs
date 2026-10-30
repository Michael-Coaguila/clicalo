using Clicalo.Application.Ports;

namespace Clicalo.Platform.Windows.Elevation;

/// <summary>Starts an executable elevated, with the UAC prompt (<c>ShellExecuteEx</c> «runas»); tests use a fake.</summary>
internal interface IElevationLauncher
{
    /// <summary>Asks Windows to start <paramref name="executable"/> as administrator. Blocks until Windows answers.</summary>
    /// <param name="executable">The verified installed executable.</param>
    /// <param name="arguments">Its arguments.</param>
    ElevationOutcome Launch(string executable, string arguments);
}
