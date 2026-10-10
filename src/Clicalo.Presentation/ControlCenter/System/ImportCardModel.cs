namespace Clicalo.Presentation.ControlCenter.SystemSection;

/// <summary>
/// The question of Importar (COP-002), shown after the file was chosen and read: the summary of the file, its warnings,
/// and [impMerge] or [impReplace] (two taps).
/// </summary>
/// <param name="Title">[impT].</param>
/// <param name="Summary">Profiles, shortcuts and version of the file.</param>
/// <param name="Warning">Texts that cannot be read on this machine; empty for none.</param>
/// <param name="Merge">[impMerge].</param>
/// <param name="MergeDescription">[impMergeD].</param>
/// <param name="Replace">[impReplace], or [confirmB] while armed.</param>
/// <param name="ReplaceDescription">[impReplaceD].</param>
/// <param name="ReplaceArmed">Whether the first tap on Reemplazar armed it.</param>
public sealed record ImportCardModel(
    string Title,
    string Summary,
    string Warning,
    string Merge,
    string MergeDescription,
    string Replace,
    string ReplaceDescription,
    bool ReplaceArmed
);
