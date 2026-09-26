namespace Clicalo.Domain.Catalog;

/// <summary>
/// Size-independent layout measures in logical pixels, generated as <see cref="PanelSizes.Layout"/> from the
/// <c>layout</c> section of <c>data/catalogs/sizes.json</c> (docs/04). Each property is named after its path.
/// </summary>
public sealed record LayoutMetrics
{
    /// <summary>Smallest touch target of any interactive control (REG-02).</summary>
    public required int MinTouchTargetPx { get; init; }

    /// <summary>Smallest text size at 100 % text scale (TEM-007).</summary>
    public required int MinTextPx { get; init; }

    /// <summary>Tile height grows by (scale − 1) × label size × this factor (CUA-011).</summary>
    public required double TextScaleTileGrowth { get; init; }

    /// <summary>Panel width = max(this, cols·w + (cols−1)·gap + chrome) (PAN-002).</summary>
    public required int PanelMinWidthPx { get; init; }

    /// <summary>Horizontal chrome added to the grid width (PAN-002).</summary>
    public required int PanelChromeWidthPx { get; init; }

    /// <summary>Margin kept to the edges of the work area (PAN-002).</summary>
    public required int PanelWorkAreaMarginPx { get; init; }

    /// <summary>Space kept above the bottom edge of the work area (PAN-002).</summary>
    public required int PanelBottomMarginPx { get; init; }

    /// <summary>Initial x = work area width − panel width − this (PAN-002).</summary>
    public required int PanelInitialRightOffsetPx { get; init; }

    /// <summary>Initial y of the panel (PAN-002).</summary>
    public required int PanelInitialTopPx { get; init; }

    /// <summary>Largest grid row preference (CUA-001).</summary>
    public required int PanelMaxGridRows { get; init; }

    /// <summary>Compact tiles are this much lower (VCO-001).</summary>
    public required int PanelCompactTileReductionPx { get; init; }

    /// <summary>Always visible row capacity in the Compact view (FIJ-003).</summary>
    public required int PanelCompactStripCapacity { get; init; }

    /// <summary>Width of the drag handle (PAN-004).</summary>
    public required int PanelDragHandleWidthPx { get; init; }

    /// <summary>Visual size of the Auto/Fixed button (CAB-003).</summary>
    public required int PanelAutoFixedButtonPx { get; init; }

    /// <summary>Minimum height of the notice bar (AVI-001).</summary>
    public required int PanelNoticeBarHeightPx { get; init; }

    /// <summary>Width of the active page dot (CUA-004).</summary>
    public required int PanelPageDotActiveWidthPx { get; init; }

    /// <summary>Size of an inactive page dot (CUA-004).</summary>
    public required int PanelPageDotSizePx { get; init; }

    /// <summary>Height of a profile tile of the profile grid (SEL-003).</summary>
    public required int PanelProfileTileHeightPx { get; init; }

    /// <summary>Height of a context menu row (CUA-014).</summary>
    public required int PanelContextMenuRowHeightPx { get; init; }

    /// <summary>Visual size of the delete button in edit mode (CUA-012).</summary>
    public required int PanelEditDeleteButtonPx { get; init; }

    /// <summary>Diameter of the minimized bubble (BUR-001).</summary>
    public required int BubbleDiameterPx { get; init; }

    /// <summary>Icon of the minimized bubble (BUR-001).</summary>
    public required int BubbleIconPx { get; init; }

    /// <summary>Thickness of the closed edge bar handle (PES-001).</summary>
    public required int DockHandleThicknessPx { get; init; }

    /// <summary>Length of the handle on the left and right edges (PES-001).</summary>
    public required int DockHandleVerticalLengthPx { get; init; }

    /// <summary>Length of the handle on the top and bottom edges (PES-001).</summary>
    public required int DockHandleHorizontalLengthPx { get; init; }

    /// <summary>Space left free for the app scroll bar on the right edge (PES-001).</summary>
    public required int DockScrollbarGutterPx { get; init; }

    /// <summary>Lowest handle position along its edge, in percent (PES-001).</summary>
    public required int DockHandlePositionMinPercent { get; init; }

    /// <summary>Highest handle position along its edge, in percent (PES-001).</summary>
    public required int DockHandlePositionMaxPercent { get; init; }
}
