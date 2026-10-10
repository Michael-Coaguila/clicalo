using Clicalo.Domain.Catalog;
using Clicalo.Domain.Messages;

namespace Clicalo.Application.Interaction;

/// <summary>
/// One notice of the product (AVI-001, blueprint §8.2): its text, kept as a message and localized when it is painted
/// (IDI-001), its icon, whether it is a warning, how the <see cref="NoticeQueue"/> treats it and whether it offers
/// [cancel]. The notice bar of the panel, the notice surface of the Tab view (PES-014) and the status bar of the control
/// center (CCM-003) all paint the same one.
/// </summary>
/// <param name="Text">The text.</param>
/// <param name="Icon">Its Material Symbols icon.</param>
/// <param name="Warning">A warning (assertive) rather than a notice (polite).</param>
/// <param name="Kind">Plain, with [undo] or a safety notice (AVI-002).</param>
/// <param name="CanCancel">It offers [cancel] (the capture mode of a binding, ATJ-008).</param>
public sealed record Notice(
    Message Text,
    IconRef Icon,
    bool Warning = false,
    NoticeKind Kind = NoticeKind.Normal,
    bool CanCancel = false
)
{
    /// <summary>Whether it offers [undo] (AVI-003).</summary>
    public bool CanUndo => Kind == NoticeKind.Undo;
}
