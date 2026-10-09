using System.Diagnostics;
using System.Globalization;
using Clicalo.Domain.Timing;
using Clicalo.Platform.Windows.Elevation;

namespace Clicalo.App.Lifecycle;

/// <summary>
/// The elevated side of «Reabrir como administrador» (blueprint §3.3, «Relanzamiento elevado», user decision D7): an
/// instance started with <c>--handover=&lt;pid&gt;</c> waits up to <c>Timings.App.HandoverMutexWait</c> for that
/// process to end (it releases everything, flushes and frees the single-instance mutex on its way out) before it takes
/// the mutex itself. Nothing is passed between the two: the document on disk is the state.
/// </summary>
internal static class ElevationHandover
{
    /// <summary>Waits for the instance named by <c>--handover</c>, if any.</summary>
    /// <param name="arguments">The command line.</param>
    public static void WaitForPrevious(IReadOnlyList<string> arguments)
    {
        if (ProcessId(arguments) is not { } id || id == Environment.ProcessId)
        {
            return;
        }

        try
        {
            using var previous = Process.GetProcessById(id);
            _ = previous.WaitForExit(Timings.App.HandoverMutexWait);
        }
        catch (ArgumentException)
        {
            // Already gone.
        }
        catch (InvalidOperationException)
        {
            // Already gone.
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Not visible to this process: the single-instance check decides.
        }
    }

    /// <summary>The process id of <c>--handover=&lt;pid&gt;</c>, or null.</summary>
    /// <param name="arguments">The command line.</param>
    internal static int? ProcessId(IReadOnlyList<string> arguments)
    {
        var prefix = ElevatedRelaunch.HandoverOption + "=";
        foreach (var argument in arguments)
        {
            if (
                argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(
                    argument.AsSpan(prefix.Length),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var id
                )
                && id > 0
            )
            {
                return id;
            }
        }

        return null;
    }
}
