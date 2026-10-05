namespace Clicalo.Domain.PanelLayout;

/// <summary>Which parts of the body of the panel show, in the order of PAN-007.</summary>
/// <param name="AdminNotice">The administrator notice (EJE-013).</param>
/// <param name="AlwaysVisibleRow">The Always visible row (FIJ-001).</param>
/// <param name="AlwaysVisibleLabel">Its «📌 SIEMPRE VISIBLE» label (FIJ-001: hidden in Compact).</param>
/// <param name="StickyRow">The sticky modifiers row (FIJ-005).</param>
/// <param name="Selector">The profile selector (SEL-001).</param>
/// <param name="PickerGrid">The profile grid (SEL-003).</param>
/// <param name="EmptyProfile">The empty profile card (CUA-010).</param>
/// <param name="Pager">◀, the page dots and ▶ under the grid (CUA-004, Full view).</param>
/// <param name="NoticeBar">The notice bar (AVI-001; in Compact only with a notice or something to repeat).</param>
/// <param name="Repeat">↻ Repeat in the notice bar (AVI-004).</param>
public sealed record BodyLayers(
    bool AdminNotice,
    bool AlwaysVisibleRow,
    bool AlwaysVisibleLabel,
    bool StickyRow,
    bool Selector,
    bool PickerGrid,
    bool EmptyProfile,
    bool Pager,
    bool NoticeBar,
    bool Repeat
);
