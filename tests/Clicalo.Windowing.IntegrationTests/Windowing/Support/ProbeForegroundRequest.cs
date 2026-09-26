using System.Globalization;
using System.IO;
using System.Reflection;
using Clicalo.TestKit.Windows.Probe;

namespace Clicalo.Windowing.IntegrationTests.Windowing.Support;

/// <summary>
/// Asks InputProbe to call <c>SetForegroundWindow</c> on another window: the <c>foreground</c> command with
/// <c>hwnd</c>, which the probe already understands (tools/InputProbe/README.md) but <see cref="InputProbeSession"/>
/// does not expose yet. It writes the command line through the session's own pipe writer, under its write gate.
/// </summary>
/// <remarks>
/// Temporary glue for <c>ActivationGuardNegativeTests</c>: <c>tests/Clicalo.TestKit.Windows/Probe/</c> is not a path of
/// the windowing package, so the public <c>InputProbeSession.RequestForegroundAsync(window, …)</c> is a shared change
/// (docs/testing/spikes/S1.md). When it exists, this file is deleted and the test calls it.
/// </remarks>
internal static class ProbeForegroundRequest
{
    private static long _nextId = 1L << 40;

    /// <summary>Makes the probe call <c>SetForegroundWindow(<paramref name="window"/>)</c> and returns its answer.</summary>
    public static async Task<ProbeForegroundEvent> RequestForegroundAsync(
        this InputProbeSession probe,
        nint window,
        TimeSpan timeout,
        CancellationToken cancellationToken
    )
    {
        var writer = Field<StreamWriter>(probe, "_writer");
        var gate = Field<SemaphoreSlim>(probe, "_writeGate");
        var id = Interlocked.Increment(ref _nextId);
        var line = string.Create(
            CultureInfo.InvariantCulture,
            $$"""{"cmd":"foreground","id":{{id}},"hwnd":{{(long)window}}}"""
        );
        var cursor = probe.Cursor;
        await gate.WaitAsync(cancellationToken);
        try
        {
            await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
            await writer.FlushAsync(cancellationToken);
        }
        finally
        {
            gate.Release();
        }

        var events = await probe.WaitForAsync(
            cursor,
            received => received.OfType<ProbeForegroundEvent>().Any(answer => answer.Id == id),
            timeout,
            cancellationToken
        );
        return events.OfType<ProbeForegroundEvent>().First(answer => answer.Id == id);
    }

    private static T Field<T>(InputProbeSession probe, string name)
        where T : class =>
        typeof(InputProbeSession)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(probe) as T
        ?? throw new InvalidOperationException(
            "InputProbeSession no longer has the private field "
                + name
                + ": call InputProbeSession.RequestForegroundAsync and delete this glue."
        );
}
