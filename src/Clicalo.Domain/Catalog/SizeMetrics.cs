namespace Clicalo.Domain.Catalog;

/// <summary>
/// Measures of one panel size (S, M or L) in logical pixels, generated into <see cref="PanelSizes"/> from
/// <c>data/catalogs/sizes.json</c> (docs/04). Each property is named after the path of its value in the file.
/// Text sizes are design values: layout raises them to <see cref="LayoutMetrics.MinTextPx"/> (TEM-007).
/// </summary>
public sealed record SizeMetrics
{
    /// <summary>Size these measures belong to.</summary>
    public required PanelSize Size { get; init; }

    /// <summary>Width of a shortcut tile (CUA-007).</summary>
    public required int TileWidthPx { get; init; }

    /// <summary>Height of a shortcut tile at 100 % text scale (CUA-007, CUA-011).</summary>
    public required int TileHeightPx { get; init; }

    /// <summary>Icon of a shortcut tile.</summary>
    public required int TileIconPx { get; init; }

    /// <summary>Name of a shortcut tile.</summary>
    public required int TileLabelPx { get; init; }

    /// <summary>Key line of a shortcut tile.</summary>
    public required int TileKeysPx { get; init; }

    /// <summary>Gap between tiles (CUA-001).</summary>
    public required int GapPx { get; init; }

    /// <summary>Width of the header buttons (CAB-001).</summary>
    public required int HeaderButtonWidthPx { get; init; }

    /// <summary>Height of the header buttons (CAB-001).</summary>
    public required int HeaderButtonHeightPx { get; init; }

    /// <summary>Height of the Always visible row (FIJ-002).</summary>
    public required int StripHeightPx { get; init; }

    /// <summary>Icon of the Always visible row.</summary>
    public required int StripIconPx { get; init; }

    /// <summary>Name of the Always visible row.</summary>
    public required int StripLabelPx { get; init; }

    /// <summary>Shortcuts the Always visible row holds before paging (FIJ-003).</summary>
    public required int StripCapacity { get; init; }

    /// <summary>Grid rows when the row preference is Auto (CUA-001).</summary>
    public required int AutoRows { get; init; }

    /// <summary>Width of the open vertical edge bar (PES-005).</summary>
    public required int DockBarWidthPx { get; init; }

    /// <summary>Height of the open horizontal edge bar (PES-005).</summary>
    public required int DockBarHeightPx { get; init; }

    /// <summary>Height of a tile of the vertical edge bar (PES-007).</summary>
    public required int DockVerticalTileHeightPx { get; init; }

    /// <summary>Width of a tile of the horizontal edge bar (PES-007).</summary>
    public required int DockHorizontalTileWidthPx { get; init; }

    /// <summary>Height of a tile of the horizontal edge bar (PES-007).</summary>
    public required int DockHorizontalTileHeightPx { get; init; }

    /// <summary>Icon of an edge bar tile (PES-007).</summary>
    public required int DockTileIconPx { get; init; }
}
