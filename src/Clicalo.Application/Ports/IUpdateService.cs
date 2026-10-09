using Clicalo.Application.Confirmation;

namespace Clicalo.Application.Ports;

/// <summary>
/// Updates of the installed copy (ACT-001 to ACT-005, NFR-010, ADR-0027): checks the channel, downloads with the
/// verification of the package, installs only after a backup and when the panel has not been used for a while (or
/// when the person asks), and goes back to the previous version during <c>Timings.Backups.PreviousVersionRetention</c>.
/// </summary>
public interface IUpdateService
{
    /// <summary>What the «Actualizaciones» tab shows.</summary>
    UpdateStatus Status { get; }

    /// <summary>Raised after <see cref="Status"/> changes, on any thread.</summary>
    event EventHandler? StatusChanged;

    /// <summary>[Buscar actualizaciones], or [Reintentar] after an error.</summary>
    /// <param name="cancellationToken">Cancels the check.</param>
    Task CheckAsync(CancellationToken cancellationToken);

    /// <summary>[Instalar ahora]: downloads, backs up and restarts into the new version.</summary>
    /// <param name="cancellationToken">Cancels before the restart.</param>
    Task InstallAsync(CancellationToken cancellationToken);

    /// <summary>[Volver] confirmed with two taps (REG-04): downloads the previous version and restarts into it.</summary>
    /// <param name="token">The proof of the second tap.</param>
    /// <param name="cancellationToken">Cancels before the restart.</param>
    Task RollbackAsync(ConfirmationToken token, CancellationToken cancellationToken);

    /// <summary>[Listo] of «Actualizado»: back to «al día».</summary>
    void Acknowledge();
}
