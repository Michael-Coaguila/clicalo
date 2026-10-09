namespace Clicalo.Presentation.ControlCenter.Shortcuts;

/// <summary>A chip of an open app (ATJ-006, PRB-003, EDI-014).</summary>
/// <param name="Process">Its process.</param>
/// <param name="Name">Its name.</param>
/// <param name="Selected">Whether it is the bound app, the chosen one or the target.</param>
public sealed record AppChip(string Process, string Name, bool Selected);
