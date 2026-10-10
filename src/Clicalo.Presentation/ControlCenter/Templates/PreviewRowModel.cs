using Clicalo.Presentation.ControlCenter.Editor;

namespace Clicalo.Presentation.ControlCenter.Templates;

/// <summary>A row of the preview (PLA-015).</summary>
/// <param name="Index">Its position.</param>
/// <param name="Icon">Its icon.</param>
/// <param name="Name">Its name.</param>
/// <param name="Foot">Its keys, or [alreadyIn].</param>
/// <param name="AlreadyIn">Whether the installed profile has it.</param>
/// <param name="Checked">Whether it will be installed.</param>
/// <param name="Editing">Whether its name field is open.</param>
/// <param name="Warning">[riskyMark] or [dangerMark].</param>
/// <param name="RenameName">[rename].</param>
/// <param name="DictateName">The accessible name of the dictation button of the name field (PLA-015, ACC-011).</param>
/// <param name="KeysName">[changeKeys]; null when the row has no combination to change (PLA-016).</param>
/// <param name="Combo">The combination box of the row while its keys are being changed (PLA-016).</param>
public sealed record PreviewRowModel(
    int Index,
    string Icon,
    string Name,
    string Foot,
    bool AlreadyIn,
    bool Checked,
    bool Editing,
    string? Warning,
    string RenameName,
    string DictateName,
    string? KeysName,
    ComboModel? Combo
);
