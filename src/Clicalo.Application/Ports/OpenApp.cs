using Clicalo.Domain.Primitives;

namespace Clicalo.Application.Ports;

/// <summary>
/// An app open on the desktop, as the Control Center offers it (ATJ-006 [linkOpenApps], PRB-003 «Probar en»): its
/// process, a name to show and its main window. Never logged with its window title (LOG-001).
/// </summary>
/// <param name="Process">The executable, for example <c>WINWORD.EXE</c>.</param>
/// <param name="Name">The name to show, for example «Word»: the file description, or the executable without «.exe».</param>
/// <param name="Window">Its main top-level window.</param>
/// <param name="Elevated">Whether it runs as administrator (or could not be inspected, which counts as elevated).</param>
/// <param name="ExecutablePath">The full path of its executable, when it could be read; null otherwise.</param>
public sealed record OpenApp(
    ProcessName Process,
    string Name,
    WindowToken Window,
    bool Elevated,
    string? ExecutablePath
);
