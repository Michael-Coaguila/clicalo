using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.ControlCenter.SystemSection;

/// <summary>The tab «Copias de seguridad» (COP-002 to COP-004).</summary>
/// <param name="Folder">The folder of the backups, with <c>%APPDATA%</c> in place of the user's folder.</param>
/// <param name="Format">[bakFormat].</param>
/// <param name="BackupNow">[backupNow].</param>
/// <param name="Export">[export].</param>
/// <param name="Import">[import].</param>
/// <param name="Busy">Whether a backup, an export or an import is running: the buttons wait.</param>
/// <param name="ImportCard">The question after choosing a file; null when closed.</param>
/// <param name="Auto">«Copia automática».</param>
/// <param name="History">[backupHist].</param>
/// <param name="Rows">The backups, newest first.</param>
/// <param name="Empty">[backupNone], shown when there are none.</param>
public sealed record BackupsModel(
    string Folder,
    string Format,
    string BackupNow,
    string Export,
    string Import,
    bool Busy,
    ImportCardModel? ImportCard,
    SwitchModel Auto,
    string History,
    ValueList<BackupRowModel> Rows,
    string Empty
);
