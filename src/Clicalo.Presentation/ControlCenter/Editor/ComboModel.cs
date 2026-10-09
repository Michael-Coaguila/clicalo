using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.ControlCenter.Editor;

/// <summary>The combination box and the key picker (EDI-007 to EDI-009).</summary>
/// <param name="Title">[keys].</param>
/// <param name="Replacing">[replaceMsg] while a repeated combination is replaced.</param>
/// <param name="KeepOldText">[keepOld].</param>
/// <param name="Empty">[comboEmpty] when there are no keys.</param>
/// <param name="Chips">The keys in press order.</param>
/// <param name="Count">[comboN] or [comboNone].</param>
/// <param name="HasKeys">Whether backspace and [clearKeys] apply.</param>
/// <param name="BackName">[backKey].</param>
/// <param name="ClearText">[clearKeys].</param>
/// <param name="Warning">The tone of the warning.</param>
/// <param name="WarningText">[blockedB] or [blockedS].</param>
/// <param name="StepEditing">[stepEditMsg] while a macro step is edited.</param>
/// <param name="DoneText">[done].</param>
/// <param name="Modifiers">Ctrl, Alt, Shift and Win.</param>
/// <param name="Groups">The groups.</param>
/// <param name="Cells">The keys of the chosen group.</param>
/// <param name="Columns">7, 6, or 0 for cells of at least 92.</param>
/// <param name="OrderHint">[orderHint2].</param>
public sealed record ComboModel(
    string Title,
    string? Replacing,
    string KeepOldText,
    string? Empty,
    ValueList<KeyChip> Chips,
    string Count,
    bool HasKeys,
    string BackName,
    string ClearText,
    WarningTone Warning,
    string? WarningText,
    string? StepEditing,
    string DoneText,
    ValueList<KeyCell> Modifiers,
    ValueList<KeyGroupTab> Groups,
    ValueList<KeyCell> Cells,
    int Columns,
    string OrderHint
);
