namespace Clicalo.Domain.PanelLayout;

/// <summary>
/// The profile selector (SEL-001, SEL-002): ★ Frequents is filled while Frequents is in view; the profile button shows
/// the profile in view, or the return profile from Frequents, and ends in ▾, ▴ or ↶.
/// </summary>
public static class SelectorRules
{
    /// <summary>The end mark of the profile button.</summary>
    /// <param name="frequents">Frequents is in view.</param>
    /// <param name="pickerOpen">The profile grid is open.</param>
    public static SelectorCaret Caret(bool frequents, bool pickerOpen) =>
        frequents ? SelectorCaret.Return
        : pickerOpen ? SelectorCaret.Collapse
        : SelectorCaret.Expand;

    /// <summary>How the profile button is filled.</summary>
    /// <param name="frequents">Frequents is in view.</param>
    /// <param name="pickerOpen">The profile grid is open.</param>
    public static SelectorLook Look(bool frequents, bool pickerOpen) =>
        pickerOpen ? SelectorLook.Open
        : frequents ? SelectorLook.Frequents
        : SelectorLook.Active;

    /// <summary>
    /// What a tap on the profile button does (SEL-002): from Frequents it returns to the profile it shows; otherwise
    /// it opens or closes the profile grid.
    /// </summary>
    /// <param name="frequents">Frequents is in view.</param>
    public static SelectorTap Tap(bool frequents) =>
        frequents ? SelectorTap.ReturnFromFrequents : SelectorTap.TogglePicker;
}
