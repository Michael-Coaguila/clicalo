using Clicalo.Domain.Catalog;
using Clicalo.Domain.Messages;

namespace Clicalo.Presentation.Panel;

/// <summary>
/// The notice on show in the notice bar (AVI-001): its text, kept as a message and localized when painted (§8.2), its
/// icon and tone, and whether [undo] applies (AVI-003). Which notice shows and for how long is the
/// <c>NoticeQueue</c>'s decision (AVI-002), not the bar's.
/// </summary>
/// <param name="Text">The text.</param>
/// <param name="Icon">Its Material Symbols icon.</param>
/// <param name="Tone">Notice or warning.</param>
/// <param name="CanUndo">The notice belongs to an operation that can be undone and the stack is not empty (AVI-003).</param>
/// <param name="CanCancel">The notice offers [cancel] (the capture mode of a binding, ATJ-008).</param>
public sealed record PanelNotice(
    Message Text,
    IconRef Icon,
    NoticeTone Tone,
    bool CanUndo = false,
    bool CanCancel = false
);
