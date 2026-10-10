using Clicalo.Application.Confirmation;
using Clicalo.Application.Ports;
using Clicalo.Infrastructure.Ai;
using Clicalo.Infrastructure.Persistence;
using Clicalo.Platform.Windows.Secrets;
using Clicalo.Platform.Windows.Startup;
using Clicalo.Presentation.ControlCenter.SystemSection;

namespace Clicalo.App.Lifecycle;

/// <summary>
/// «Desinstalar Clícalo» (NFR-010, proposal P6, ADR-0029), in two halves:
/// <list type="bullet">
/// <item>in the running instance (<see cref="UninstallAsync"/>): with «Borrar también mis atajos y ajustes» the request
/// is written as a marker (<see cref="UninstallDataWipe"/>); then the instance ends by its only way out (release all,
/// flush, REG-03 and REG-08) and starts the uninstaller as it leaves;</item>
/// <item>in the uninstaller's hook (<see cref="OnUninstalling"/>, <c>Program.Main</c>), which cannot show UI: the
/// «Iniciar con Windows» entry is removed, and the data folders and the saved AI key are deleted only if the marker
/// is there. From Windows Settings there is never a marker, so that way always keeps the data.</item>
/// </list>
/// </summary>
/// <param name="available">Whether this copy is the installed one and its uninstaller is in place.</param>
/// <param name="start">Starts the uninstaller of the installed copy; false when Windows refused.</param>
/// <param name="locations">The data folders.</param>
/// <param name="writer">Writes the marker.</param>
/// <param name="exitThen">Ends this instance cleanly and runs the action right before the process leaves.</param>
internal sealed class SystemUninstall(
    Func<bool> available,
    Func<bool> start,
    DataLocations locations,
    IAtomicFileWriter writer,
    Func<Action, Task> exitThen
) : ISystemUninstall
{
    /// <inheritdoc />
    public bool IsAvailable => available();

    /// <summary>Velopack's uninstall hook: at most 30 s, no UI, and it never throws.</summary>
    public static void OnUninstalling()
    {
        StartupRegistration.RemoveForUninstall();
        try
        {
            if (UninstallDataWipe.RunIfRequested(DataLocations.ForCurrentUser()))
            {
                // The data includes the AI key the person saved in the Credential Manager (ADR-0014).
                _ = new CredentialKeyStore(AiServices.CredentialTarget).Delete();
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Uninstalling goes on: what could not be deleted stays in the data folder.
            _ = ex;
        }
    }

    /// <inheritdoc />
    public async Task<bool> UninstallAsync(
        bool deleteData,
        ConfirmationToken token,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(token);
        if (
            !string.Equals(
                token.Subject.Operation,
                ISystemUninstall.Operation,
                StringComparison.Ordinal
            ) || !available()
        )
        {
            return false;
        }

        if (deleteData)
        {
            var marked = await writer
                .WriteAsync(
                    UninstallDataWipe.MarkerPath(locations),
                    UninstallDataWipe.Marker,
                    cancellationToken
                )
                .ConfigureAwait(true);
            if (marked.IsFailure)
            {
                return false;
            }
        }
        else
        {
            UninstallDataWipe.Withdraw(locations);
        }

        await exitThen(() =>
            {
                if (!start())
                {
                    // Nothing uninstalls: the request must not wait for an uninstall from Windows Settings.
                    UninstallDataWipe.Withdraw(locations);
                }
            })
            .ConfigureAwait(true);
        return true;
    }
}
