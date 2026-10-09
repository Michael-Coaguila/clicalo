namespace Clicalo.Domain.PanelLayout;

/// <summary>What decides which parts of the body of the panel show (<see cref="BodyLayerRules"/>).</summary>
/// <param name="Settings">The layout settings.</param>
/// <param name="SearchingWithText">The search is open with text (PAN-008).</param>
/// <param name="Cramped">The rows hide for lack of space with an alert (<see cref="CrampedRule"/>, CUA-003).</param>
/// <param name="Frequents">Frequents is in view.</param>
/// <param name="PickerOpen">The profile grid was opened (SEL-002).</param>
/// <param name="ListCount">Shortcuts of the list in view.</param>
/// <param name="PageCount">Pages of the grid.</param>
/// <param name="HasNotice">A notice is showing (AVI-001).</param>
/// <param name="CanRepeat">There is a last action to repeat (AVI-004).</param>
/// <param name="EditMode">The panel is in edit mode (CUA-012: no Repeat).</param>
/// <param name="ElevatedTarget">The foreground app is elevated and Clícalo is not (EJE-013).</param>
public sealed record BodyLayerInputs(
    PanelLayoutSettings Settings,
    bool SearchingWithText,
    bool Cramped,
    bool Frequents,
    bool PickerOpen,
    int ListCount,
    int PageCount,
    bool HasNotice,
    bool CanRepeat,
    bool EditMode,
    bool ElevatedTarget
);
