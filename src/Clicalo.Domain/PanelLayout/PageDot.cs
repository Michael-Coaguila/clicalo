using System.Runtime.InteropServices;

namespace Clicalo.Domain.PanelLayout;

/// <summary>One page dot of the pager (CUA-004): the active one is 24 wide, the others 10.</summary>
/// <param name="Page">The page it jumps to, from 0.</param>
/// <param name="IsActive">Whether it is the page in view.</param>
/// <param name="WidthPx">Visual width; the touch target is 44 whatever this is.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct PageDot(int Page, bool IsActive, int WidthPx);
