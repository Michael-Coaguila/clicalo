using System.Runtime.InteropServices;

namespace Clicalo.Platform.Windows.Tray;

/// <summary>What the tray icon and its menu show (BUR-003, BUR-004).</summary>
/// <param name="PanelVisible">Whether the panel is on screen.</param>
/// <param name="AnythingHeld">Whether the engine holds anything: «Soltar todo» is enabled.</param>
/// <param name="Paused">Whether Clícalo is paused: nothing is sent until «Reanudar».</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct TrayState(bool PanelVisible, bool AnythingHeld, bool Paused);
