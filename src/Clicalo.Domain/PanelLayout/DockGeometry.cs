using Clicalo.Domain.Catalog;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.Settings;

namespace Clicalo.Domain.PanelLayout;

/// <summary>
/// Where the surfaces of the Tab view go (docs/04 «Vista pestaña», PES-001, PES-002, PES-005, PES-010, PES-011,
/// PES-013, PES-015), in physical pixels inside the work area of the monitor: the closed handle, the open bar, the
/// windows beside it and the floating «Release all». Measures in logical pixels come from <c>sizes.json</c> and from
/// the prototype; the result is scaled with the monitor. Pure.
/// </summary>
public static class DockGeometry
{
    /// <summary>The open bar never comes closer than this to both ends of a vertical edge, together (PES-005).</summary>
    public const int VerticalBarReservePx = 24;

    /// <summary>The open bar never comes closer than this to both ends of a horizontal edge, together (PES-005).</summary>
    public const int HorizontalBarReservePx = 40;

    /// <summary>A horizontal bar stays this far from its edge (PES-005).</summary>
    public const int HorizontalBarInsetPx = 12;

    /// <summary>The floating «Release all» of the open bar stays this far from a side edge or the top (PES-013).</summary>
    public const int PanicInsetPx = 110;

    /// <summary>On the bottom edge it stays this far from the bottom (PES-013).</summary>
    public const int PanicBottomInsetPx = 180;

    /// <summary>The «Pinned» and «Sticky keys» windows open this far from their button, toward the screen (PES-010).</summary>
    public const int ButtonWindowGapPx = 16;

    /// <summary>The profile grid and the guide open this far from the bar (PES-011, PES-015).</summary>
    public const int BarWindowGapPx = 8;

    /// <summary>The windows beside the bar are at most the work area minus this, high (PES-010, PES-011).</summary>
    public const int SideWindowReservePx = 140;

    /// <summary>Whether <paramref name="side"/> is the left or the right edge: the handle and the bar are vertical.</summary>
    /// <param name="side">The edge.</param>
    public static bool IsVertical(DockSide side) => side is DockSide.Left or DockSide.Right;

    /// <summary>
    /// The window of the closed handle (PES-001): it draws 32 × 116 on the left and right edges or 128 × 32 on the top
    /// and bottom ones, but takes a touch 44 deep (REG-02, ACC-002), so the window is as thick as the touch target and
    /// the handle is drawn against the screen edge inside it. It is centered at <paramref name="percent"/> of the edge
    /// (kept within 8–92 %), against the edge of the work area; on the right edge with <paramref name="gutter"/>, 18 px
    /// away from it for the scroll bar of the app.
    /// </summary>
    /// <param name="side">The edge.</param>
    /// <param name="percent">The position saved for that edge.</param>
    /// <param name="monitor">The monitor of the panel.</param>
    /// <param name="gutter">«Respect the scroll bar» (docs/02 <c>gutter</c>).</param>
    public static PhysicalRect Handle(
        DockSide side,
        int percent,
        DisplayMonitor monitor,
        bool gutter
    )
    {
        ArgumentNullException.ThrowIfNull(monitor);
        var layout = PanelSizes.Layout;
        var work = monitor.WorkArea;
        var p = ClampPercent(percent) / 100d;
        var thickness = monitor.ToPhysical(
            Math.Max(layout.DockHandleThicknessPx, layout.MinTouchTargetPx)
        );
        if (IsVertical(side))
        {
            var length = monitor.ToPhysical(layout.DockHandleVerticalLengthPx);
            var top = (int)Math.Round(work.Top + (work.Height * p) - (length / 2d));
            top = Math.Clamp(top, work.Top, Math.Max(work.Top, work.Bottom - length));
            var left =
                side == DockSide.Left
                    ? work.Left
                    : work.Right - thickness - GutterPx(side, gutter, monitor);
            return new PhysicalRect(left, top, thickness, length);
        }

        var width = monitor.ToPhysical(layout.DockHandleHorizontalLengthPx);
        var x = (int)Math.Round(work.Left + (work.Width * p) - (width / 2d));
        x = Math.Clamp(x, work.Left, Math.Max(work.Left, work.Right - width));
        var y = side == DockSide.Top ? work.Top : work.Bottom - thickness;
        return new PhysicalRect(x, y, width, thickness);
    }

    /// <summary>
    /// Whether a contact that went down at <paramref name="point"/> may start dragging the handle (PES-002,
    /// EC-PES-03): not on the pixel of the handle that touches its screen edge, where Windows starts its own edge
    /// gestures (notifications, widgets). A tap there still opens the bar.
    /// </summary>
    /// <param name="side">The edge of the handle.</param>
    /// <param name="handle">The handle on screen.</param>
    /// <param name="point">Where the contact went down.</param>
    public static bool HandleDragStartsAt(
        DockSide side,
        PhysicalRect handle,
        PhysicalPoint point
    ) =>
        side switch
        {
            DockSide.Left => point.X > handle.Left,
            DockSide.Top => point.Y > handle.Top,
            DockSide.Bottom => point.Y < handle.Bottom - 1,
            _ => point.X < handle.Right - 1,
        };

    /// <summary>The handle position kept inside 8–92 % (<c>sizes.json</c>).</summary>
    /// <param name="percent">A position along the edge, in percent.</param>
    public static int ClampPercent(int percent) =>
        Math.Clamp(
            percent,
            PanelSizes.Layout.DockHandlePositionMinPercent,
            PanelSizes.Layout.DockHandlePositionMaxPercent
        );

    /// <summary>
    /// The handle position after a drag (PES-002): the drag along the edge, as a share of the edge, added to the
    /// position it started from, kept inside 8–92 %. The drag across the edge does not count.
    /// </summary>
    /// <param name="side">The edge.</param>
    /// <param name="startPercent">The position when the drag started.</param>
    /// <param name="offset">How far the finger is from where it went down.</param>
    /// <param name="monitor">The monitor of the panel.</param>
    public static int PercentAfterDrag(
        DockSide side,
        int startPercent,
        PhysicalOffset offset,
        DisplayMonitor monitor
    )
    {
        ArgumentNullException.ThrowIfNull(monitor);
        var vertical = IsVertical(side);
        var span = vertical ? monitor.WorkArea.Height : monitor.WorkArea.Width;
        if (span <= 0)
        {
            return ClampPercent(startPercent);
        }

        var along = vertical ? offset.Dy : offset.Dx;
        var percent = startPercent + (along * 100d / span);
        return ClampPercent((int)Math.Round(percent, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// The longest the open bar may be along its edge (PES-005): the work area less 24 on a vertical edge, less 40 on
    /// a horizontal one.
    /// </summary>
    /// <param name="side">The edge.</param>
    /// <param name="monitor">The monitor of the panel.</param>
    public static int MaxBarLength(DockSide side, DisplayMonitor monitor)
    {
        ArgumentNullException.ThrowIfNull(monitor);
        return IsVertical(side)
            ? monitor.WorkArea.Height - monitor.ToPhysical(VerticalBarReservePx)
            : monitor.WorkArea.Width - monitor.ToPhysical(HorizontalBarReservePx);
    }

    /// <summary>
    /// The open bar (PES-005): as thick as the size says (76/88/108 wide on a vertical edge, 58/66/78 high on a
    /// horizontal one), as long as its content up to <see cref="MaxBarLength"/>, centered along its edge; against a
    /// vertical edge (18 px away on the right with <paramref name="gutter"/>), 12 px away from a horizontal one.
    /// </summary>
    /// <param name="side">The edge.</param>
    /// <param name="length">The length its content asks for, in physical pixels.</param>
    /// <param name="size">The panel size.</param>
    /// <param name="monitor">The monitor of the panel.</param>
    /// <param name="gutter">«Respect the scroll bar».</param>
    public static PhysicalRect Bar(
        DockSide side,
        int length,
        SizeMetrics size,
        DisplayMonitor monitor,
        bool gutter
    )
    {
        ArgumentNullException.ThrowIfNull(size);
        ArgumentNullException.ThrowIfNull(monitor);
        var work = monitor.WorkArea;
        var along = Math.Max(1, Math.Min(length, MaxBarLength(side, monitor)));
        if (IsVertical(side))
        {
            var width = monitor.ToPhysical(size.DockBarWidthPx);
            var top = work.Top + ((work.Height - along) / 2);
            var left =
                side == DockSide.Left
                    ? work.Left
                    : work.Right - width - GutterPx(side, gutter, monitor);
            return new PhysicalRect(left, top, width, along);
        }

        var height = monitor.ToPhysical(size.DockBarHeightPx);
        var inset = monitor.ToPhysical(HorizontalBarInsetPx);
        var x = work.Left + ((work.Width - along) / 2);
        var y = side == DockSide.Top ? work.Top + inset : work.Bottom - inset - height;
        return new PhysicalRect(x, y, along, height);
    }

    /// <summary>
    /// A window beside the bar or the handle, toward the inside of the screen (PES-010, PES-011, PES-013, PES-015):
    /// <paramref name="gap"/> away from <paramref name="anchor"/>, aligned with its start (top or left), its end
    /// (bottom or right) or its center, and kept whole inside the work area.
    /// </summary>
    /// <param name="side">The edge of the bar.</param>
    /// <param name="anchor">What the window goes beside: the bar, a button of the bar or the handle.</param>
    /// <param name="width">Width of the window, in physical pixels.</param>
    /// <param name="height">Height of the window, in physical pixels.</param>
    /// <param name="gap">Space between the anchor and the window, in physical pixels.</param>
    /// <param name="align">How it lines up with the anchor along the edge.</param>
    /// <param name="monitor">The monitor of the panel.</param>
    public static PhysicalRect Beside(
        DockSide side,
        PhysicalRect anchor,
        int width,
        int height,
        int gap,
        DockAlign align,
        DisplayMonitor monitor
    )
    {
        ArgumentNullException.ThrowIfNull(monitor);
        int left;
        int top;
        if (IsVertical(side))
        {
            left = side == DockSide.Right ? anchor.Left - gap - width : anchor.Right + gap;
            top = align switch
            {
                DockAlign.Start => anchor.Top,
                DockAlign.End => anchor.Bottom - height,
                _ => anchor.Top + ((anchor.Height - height) / 2),
            };
        }
        else
        {
            top = side == DockSide.Bottom ? anchor.Top - gap - height : anchor.Bottom + gap;
            left = align switch
            {
                DockAlign.Start => anchor.Left,
                DockAlign.End => anchor.Right - width,
                _ => anchor.Left + ((anchor.Width - width) / 2),
            };
        }

        return PanelGeometry.ClampInto(
            new PhysicalRect(left, top, width, height),
            monitor.WorkArea,
            0
        );
    }

    /// <summary>
    /// <see cref="Beside"/>, clear of the surfaces already placed there (PES-014): while the place is taken by one of
    /// <paramref name="taken"/>, the surface goes further from the edge, beside that one too. The notice surface of the
    /// Tab view uses it, so it never covers a window beside the bar nor the floating «Release all».
    /// </summary>
    /// <param name="side">The edge of the Tab view.</param>
    /// <param name="anchor">The bar, the handle or a button, in physical pixels.</param>
    /// <param name="width">The width of the surface.</param>
    /// <param name="height">The height of the surface.</param>
    /// <param name="gap">The gap between the anchor and the surface.</param>
    /// <param name="align">How it lines up along the edge.</param>
    /// <param name="monitor">The monitor.</param>
    /// <param name="taken">The surfaces already placed beside the anchor.</param>
    public static PhysicalRect BesideClear(
        DockSide side,
        PhysicalRect anchor,
        int width,
        int height,
        int gap,
        DockAlign align,
        DisplayMonitor monitor,
        IReadOnlyList<PhysicalRect> taken
    )
    {
        ArgumentNullException.ThrowIfNull(taken);
        var rect = Beside(side, anchor, width, height, gap, align, monitor);
        for (var pass = 0; pass < taken.Count; pass++)
        {
            PhysicalRect? hit = null;
            foreach (var other in taken)
            {
                if (Overlap(rect, other))
                {
                    hit = other;
                    break;
                }
            }

            if (hit is not { } covered)
            {
                break;
            }

            anchor = PhysicalRect.FromEdges(
                Math.Min(anchor.Left, covered.Left),
                Math.Min(anchor.Top, covered.Top),
                Math.Max(anchor.Right, covered.Right),
                Math.Max(anchor.Bottom, covered.Bottom)
            );
            rect = Beside(side, anchor, width, height, gap, align, monitor);
        }

        return rect;
    }

    /// <summary>
    /// The floating «Release all» of the open bar (PES-013): 110 px from a side edge or the top (180 from the bottom),
    /// centered along the edge.
    /// </summary>
    /// <param name="side">The edge of the bar.</param>
    /// <param name="width">Width of the button, in physical pixels.</param>
    /// <param name="height">Height of the button, in physical pixels.</param>
    /// <param name="monitor">The monitor of the panel.</param>
    public static PhysicalRect Panic(DockSide side, int width, int height, DisplayMonitor monitor)
    {
        ArgumentNullException.ThrowIfNull(monitor);
        var work = monitor.WorkArea;
        var inset = monitor.ToPhysical(PanicInsetPx);
        var rect = side switch
        {
            DockSide.Right => new PhysicalRect(
                work.Right - inset - width,
                work.Top + ((work.Height - height) / 2),
                width,
                height
            ),
            DockSide.Left => new PhysicalRect(
                work.Left + inset,
                work.Top + ((work.Height - height) / 2),
                width,
                height
            ),
            DockSide.Top => new PhysicalRect(
                work.Left + ((work.Width - width) / 2),
                work.Top + inset,
                width,
                height
            ),
            _ => new PhysicalRect(
                work.Left + ((work.Width - width) / 2),
                work.Bottom - monitor.ToPhysical(PanicBottomInsetPx) - height,
                width,
                height
            ),
        };
        return PanelGeometry.ClampInto(rect, work, 0);
    }

    /// <summary>The tallest a window beside the bar may be (PES-010, PES-011): the work area less 140.</summary>
    /// <param name="monitor">The monitor of the panel.</param>
    public static int MaxSideWindowHeight(DisplayMonitor monitor)
    {
        ArgumentNullException.ThrowIfNull(monitor);
        return Math.Max(1, monitor.WorkArea.Height - monitor.ToPhysical(SideWindowReservePx));
    }

    /// <summary>
    /// Shortcuts per page of the bar (PES-007): max(1, min(the preference of General, the shortcuts that fit
    /// <b>whole</b> in the measured space)).
    /// </summary>
    /// <param name="preference">4, 5, 6 or 8 (docs/02 <c>dock.perPage</c>).</param>
    /// <param name="availablePx">The space measured for the shortcuts along the bar.</param>
    /// <param name="tilePx">The length of one shortcut along the bar.</param>
    /// <param name="gapPx">The gap between two shortcuts.</param>
    public static int TilesPerPage(int preference, double availablePx, double tilePx, double gapPx)
    {
        if (tilePx <= 0 || double.IsNaN(availablePx))
        {
            return Math.Max(1, preference);
        }

        var fit = double.IsPositiveInfinity(availablePx)
            ? int.MaxValue
            : (int)Math.Floor((Math.Max(0, availablePx) + gapPx) / (tilePx + gapPx));
        return Math.Max(1, Math.Min(preference, fit));
    }

    private static bool Overlap(PhysicalRect a, PhysicalRect b) =>
        !a.IsEmpty
        && !b.IsEmpty
        && a.Left < b.Right
        && b.Left < a.Right
        && a.Top < b.Bottom
        && b.Top < a.Bottom;

    private static int GutterPx(DockSide side, bool gutter, DisplayMonitor monitor) =>
        side == DockSide.Right && gutter
            ? monitor.ToPhysical(PanelSizes.Layout.DockScrollbarGutterPx)
            : 0;
}
