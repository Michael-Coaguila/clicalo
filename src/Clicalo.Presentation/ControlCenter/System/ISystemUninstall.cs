using Clicalo.Application.Confirmation;

namespace Clicalo.Presentation.ControlCenter.SystemSection;

/// <summary>
/// «Desinstalar Clícalo» of Sistema (NFR-010, proposal P6, ADR-0029): the data is kept by default, and it is deleted
/// only when the person asks for it with two taps, after saving a copy where they choose (REG-04, REG-08). The
/// composition root implements it over the uninstaller of the installed copy.
/// </summary>
public interface ISystemUninstall
{
    /// <summary>
    /// The operation of the closed list of destructive operations (<c>architecture/destructive-operations.json</c>):
    /// the subject of its two-tap confirmation.
    /// </summary>
    const string Operation = "UninstallKeepOrDeleteData";

    /// <summary>Whether this copy was installed with the installer: only that one can be uninstalled from here.</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Uninstalls: releases every key and flushes the document, then starts the uninstaller as this instance ends.
    /// With <paramref name="deleteData"/> the uninstaller also deletes the data folders; otherwise they are kept.
    /// </summary>
    /// <param name="deleteData">Whether the data is deleted too; the caller saved a copy before.</param>
    /// <param name="token">The confirmation of the two taps (REG-04).</param>
    /// <param name="cancellationToken">Cancels before anything starts.</param>
    /// <returns>False when the uninstaller could not be started: nothing was deleted and the app goes on.</returns>
    Task<bool> UninstallAsync(
        bool deleteData,
        ConfirmationToken token,
        CancellationToken cancellationToken
    );
}
