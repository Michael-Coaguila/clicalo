using Clicalo.Application.Ports;

namespace Clicalo.Presentation.ControlCenter.SystemSection;

/// <summary>A backup of the history (COP-004): when, its kind and its own counts, and [Restaurar] with two taps.</summary>
/// <param name="Id">The backup.</param>
/// <param name="Date">«Hoy, 09:12», «Ayer, 18:40» or «22 sep, 16:27».</param>
/// <param name="Meta">«Automática · 3 perfiles · 12 atajos».</param>
/// <param name="Button">[restoreB], or [confirmB] while armed.</param>
/// <param name="Armed">Whether the first tap armed it.</param>
public sealed record BackupRowModel(BackupId Id, string Date, string Meta, string Button, bool Armed);
