using Clicalo.Domain.Catalog;
using Clicalo.Domain.Settings;
using SizeId = Clicalo.Domain.Catalog.PanelSize;
using SizeSetting = Clicalo.Domain.Settings.PanelSize;

namespace Clicalo.Domain.PanelLayout;

/// <summary>
/// The settings that shape the body of the panel (docs/04 §2–§11): size, view, columns, rows, text scale and the
/// optional rows. One immutable value, so the layout rules take it whole.
/// </summary>
/// <param name="Size">S, M or L (GEN-004).</param>
/// <param name="Compact">The Compact view (VCO-001): tiles 16 px lower, no strip label, strip capacity 4.</param>
/// <param name="Columns">Tiles per row, 2 to 4 (CUA-001).</param>
/// <param name="RowsPreference">Rows, 1 to 3, or 0 for automatic (CUA-001).</param>
/// <param name="TextScalePercent">Text scale, 100 to 150 % (CUA-011).</param>
/// <param name="ShowAlwaysVisibleRow">The Always visible row is on (<c>showStripRow</c>, FIJ-001).</param>
/// <param name="ShowSelectorRow">The profile selector row is on (<c>showTabsRow</c>, SEL-001).</param>
/// <param name="StickyRow">The sticky modifiers row is on (FIJ-005).</param>
/// <param name="VoiceNumbers">«Numbers for voice» is on (ACC-009).</param>
public sealed record PanelLayoutSettings(
    SizeSetting Size,
    bool Compact,
    int Columns,
    int RowsPreference,
    int TextScalePercent,
    bool ShowAlwaysVisibleRow,
    bool ShowSelectorRow,
    bool StickyRow,
    bool VoiceNumbers
)
{
    /// <summary>The defaults of a new document: M, Full, 3 columns, automatic rows, 100 %, both rows on.</summary>
    public static PanelLayoutSettings Default { get; } =
        new(SizeSetting.Medium, false, 3, 0, 100, true, true, false, false);

    /// <summary>The measures of <see cref="Size"/> (<c>data/catalogs/sizes.json</c>).</summary>
    public SizeMetrics Metrics =>
        PanelSizes.Get(
            Size switch
            {
                SizeSetting.Small => SizeId.S,
                SizeSetting.Large => SizeId.L,
                _ => SizeId.M,
            }
        );

    /// <summary>Whether this is size S.</summary>
    public bool IsSmall => Size == SizeSetting.Small;

    /// <summary>The layout settings of <paramref name="settings"/>.</summary>
    /// <param name="settings">The user settings.</param>
    public static PanelLayoutSettings From(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new PanelLayoutSettings(
            settings.Size,
            settings.Density == PanelDensity.Compact,
            settings.Columns,
            settings.RowsPreference,
            settings.TextScalePercent,
            settings.ShowAlwaysVisibleRow,
            settings.ShowProfileSelectorRow,
            settings.StickyModifiersRow,
            settings.VoiceNumbers
        );
    }
}
