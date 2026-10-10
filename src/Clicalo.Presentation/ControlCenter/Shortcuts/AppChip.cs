namespace Clicalo.Presentation.ControlCenter.Shortcuts;

/// <summary>A chip of an open app (ATJ-006, PRB-003, EDI-014).</summary>
/// <param name="Process">Its process.</param>
/// <param name="Name">Its name.</param>
/// <param name="Selected">Whether it is the bound app, the chosen one or the target.</param>
/// <param name="Mark">
/// [activeShort] on the app that was in front before the Control Center opened, so the person finds it at once in
/// [linkOpenApps] (ATJ-008); null on the others.
/// </param>
public sealed record AppChip(string Process, string Name, bool Selected, string? Mark = null);
