using System.Runtime.InteropServices;

namespace Clicalo.Domain.PanelLayout;

/// <summary>
/// What the page of the grid belongs to (CUA-006): when any part changes (the profile or view in view, the columns,
/// the visible rows or the panel view), the grid goes back to page 1.
/// </summary>
/// <param name="View">The list in view: a profile id, Frequents or the search.</param>
/// <param name="Columns">Columns.</param>
/// <param name="Rows">Visible rows.</param>
/// <param name="Compact">The Compact view.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct PageContext(string View, int Columns, int Rows, bool Compact);
