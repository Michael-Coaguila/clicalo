using System.Collections.Immutable;
using Clicalo.Application.Ports;
using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;

namespace Clicalo.Presentation.ControlCenter.SystemSection;

/// <summary>
/// The backups as «Copias de seguridad» uses them (COP-002 to COP-005), over the existing backup service and the
/// Persistence thread, which stays the only writer of <c>backups\</c>. The composition root implements it, with the
/// file pickers of Windows for Exportar and Importar.
/// </summary>
public interface ISystemBackups
{
    /// <summary>The folder of the backups, as it is shown.</summary>
    string Folder { get; }

    /// <summary>Every backup, newest first, with its own counts.</summary>
    /// <param name="cancellationToken">Cancels the listing.</param>
    Task<ImmutableArray<BackupInfo>> ListAsync(CancellationToken cancellationToken);

    /// <summary>[Crear copia ahora]: a manual backup of <paramref name="document"/>, written now.</summary>
    /// <param name="document">The current document.</param>
    /// <param name="cancellationToken">Cancels before the write.</param>
    Task<bool> CreateAsync(UserDocument document, CancellationToken cancellationToken);

    /// <summary>Reads and validates a backup to restore it.</summary>
    /// <param name="id">The backup.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    Task<Result<UserDocument>> ReadAsync(BackupId id, CancellationToken cancellationToken);

    /// <summary>[Exportar]: asks where, then writes a copy of <paramref name="document"/> there.</summary>
    /// <param name="document">The current document.</param>
    /// <param name="outsideData">
    /// The copy comes before deleting the data when uninstalling (NFR-010): a place inside the data folders is refused
    /// with <see cref="ExportOutcome.InsideData"/>, because the copy would be deleted with them (REG-08).
    /// </param>
    /// <param name="cancellationToken">Cancels before the write.</param>
    Task<ExportOutcome> ExportAsync(
        UserDocument document,
        bool outsideData,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// [Importar], first step (COP-002): asks for the file, reads it as untrusted content (size, schema, LOG-006) and
    /// summarizes it; null when the person cancelled the picker.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    Task<Result<ImportPick>?> PickImportAsync(CancellationToken cancellationToken);
}
