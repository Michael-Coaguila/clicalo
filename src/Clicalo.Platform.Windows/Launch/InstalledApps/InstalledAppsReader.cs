using System.Collections.Immutable;
using Clicalo.Application.UseCases.Editor;

namespace Clicalo.Platform.Windows.Launch.InstalledApps;

/// <summary>
/// The programs installed on the computer for «Elegir programa» (EDI-014): what the Start menu lists, desktop programs
/// and Store apps alike. Each read runs on its own short-lived COM thread, never on the UI thread, and only lists.
/// </summary>
public static class InstalledAppsReader
{
    /// <summary>Reads the installed programs, by name; empty when Windows does not answer.</summary>
    /// <param name="cancellationToken">Stops waiting for the answer.</param>
    public static ValueTask<ImmutableArray<InstalledProgram>> ListAsync(
        CancellationToken cancellationToken
    )
    {
        var done = new TaskCompletionSource<ImmutableArray<InstalledProgram>>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var thread = new Thread(() =>
        {
            try
            {
                _ = done.TrySetResult(AppsFolder.Read());
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // Listing is best effort: a failing shell extension never ends the process.
                _ = done.TrySetResult([]);
            }
        })
        {
            Name = "Clicalo.InstalledApps",
            IsBackground = true,
            Priority = ThreadPriority.BelowNormal,
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return new ValueTask<ImmutableArray<InstalledProgram>>(
            done.Task.WaitAsync(cancellationToken)
        );
    }
}
