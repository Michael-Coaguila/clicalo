using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.ControlCenter.Editor;

/// <summary>The repeated card of the editor (REP-004 to REP-006).</summary>
/// <param name="Head">[dupHead].</param>
/// <param name="PrevName">[prevDup].</param>
/// <param name="NextName">[nextDup].</param>
/// <param name="Expanded">Whether the rows show.</param>
/// <param name="Rows">Every appearance.</param>
/// <param name="EditingText">[editingNow].</param>
/// <param name="ConfirmText">[delConfirm].</param>
/// <param name="Advice">[dupAdvG], [dupAdvSame] or [dupAdvDiff].</param>
/// <param name="MoveText">[moveAlways], or [delConfirm] when armed; null when it does not apply.</param>
/// <param name="MoveArmed">Whether [moveAlways] was tapped once.</param>
/// <param name="FineText">[itsFine2].</param>
/// <param name="UseOtherText">[useOther].</param>
public sealed record DuplicateCardModel(
    string Head,
    string PrevName,
    string NextName,
    bool Expanded,
    ValueList<DuplicateRow> Rows,
    string EditingText,
    string ConfirmText,
    string Advice,
    string? MoveText,
    bool MoveArmed,
    string FineText,
    string UseOtherText
);
