namespace Clicalo.Domain.PanelLayout;

/// <summary>
/// The deterministic visibility of the body of the panel (PAN-008 «Acepta»; docs/04 §3 and §7–§11), evaluated without
/// an interface:
/// <list type="bullet">
/// <item>Always visible row: with <c>showStripRow</c>, without search text and without the hiding of CUA-003 (FIJ-001);
/// its label hides in Compact.</item>
/// <item>Sticky modifiers: with the switch on and without search text (FIJ-005).</item>
/// <item>Selector: with <c>showTabsRow</c>, in Full, without search text and without CUA-003 (SEL-001).</item>
/// <item>Profile grid: opened and without search text (PAN-008).</item>
/// <item>Empty profile: a profile without shortcuts, outside Frequents and the search (CUA-010).</item>
/// <item>Pager: more than one page in Full (CUA-004); the Compact one is in its bottom row.</item>
/// <item>Notice bar: always in Full; in Compact only with a notice or a last action (VCO-001). ↻ hides in edit mode.</item>
/// <item>Administrator notice: whenever the target is elevated and Clícalo is not (EJE-013); it lives with every layer.</item>
/// </list>
/// </summary>
public static class BodyLayerRules
{
    /// <summary>Evaluates the visibility of every part.</summary>
    /// <param name="inputs">The inputs.</param>
    public static BodyLayers Evaluate(BodyLayerInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var settings = inputs.Settings;
        var searching = inputs.SearchingWithText;
        var full = !settings.Compact;
        var strip = settings.ShowAlwaysVisibleRow && !searching && !inputs.Cramped;
        var empty = !searching && !inputs.Frequents && inputs.ListCount == 0;
        return new BodyLayers(
            AdminNotice: inputs.ElevatedTarget,
            AlwaysVisibleRow: strip,
            AlwaysVisibleLabel: strip && full,
            StickyRow: settings.StickyRow && !searching,
            Selector: settings.ShowSelectorRow && full && !searching && !inputs.Cramped,
            PickerGrid: inputs.PickerOpen && !searching,
            EmptyProfile: empty,
            Pager: full && !empty && inputs.PageCount > 1,
            NoticeBar: full || inputs.HasNotice || inputs.CanRepeat,
            Repeat: inputs.CanRepeat && !inputs.EditMode
        );
    }
}
