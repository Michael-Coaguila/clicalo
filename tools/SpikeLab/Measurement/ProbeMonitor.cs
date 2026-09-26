using Clicalo.TestKit.Windows.Probe;

namespace Clicalo.Tools.SpikeLab.Measurement;

/// <summary>
/// InputProbe as the target app of the voice-origin cycles of S4 (S4.md, «Regla de seguridad del laboratorio»):
/// opens it, brings it to the front by legitimate means, and counts what reaches it (F24, characters, menu) without
/// ever reading the text.
/// </summary>
internal sealed class ProbeMonitor : IAsyncDisposable
{
    private static readonly TimeSpan WaitSlice = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan ForegroundTimeout = TimeSpan.FromSeconds(2);

    private readonly LabMeasurements _measurements;
    private readonly CancellationTokenSource _stopping = new();
    private InputProbeSession? _session;
    private Task _loop = Task.CompletedTask;
    private nint _window;

    /// <summary>Creates the monitor; <see cref="OpenAsync"/> starts the probe.</summary>
    public ProbeMonitor(LabMeasurements measurements) => _measurements = measurements;

    /// <summary>The probe window, or zero when it is not open.</summary>
    public nint Window => Volatile.Read(ref _window);

    /// <summary>True while the probe runs.</summary>
    public bool IsOpen => Window != 0;

    /// <summary>Starts InputProbe (once) and tries to bring it to the front.</summary>
    public async Task OpenAsync(CancellationToken cancellationToken)
    {
        if (_session is { } open)
        {
            await open.TryBringToForegroundAsync(ForegroundTimeout, cancellationToken);
            return;
        }

        var session = await InputProbeSession.StartAsync(cancellationToken);
        _session = session;
        Volatile.Write(ref _window, session.Window);
        _measurements.Log.Add("probe", "Sonda abierta.");
        var cursor = session.Cursor;
        _loop = Task.Run(() => ReadAsync(session, cursor, _stopping.Token), CancellationToken.None);
        await session.TryBringToForegroundAsync(ForegroundTimeout, cancellationToken);
        _measurements.PostChanged();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync();
        if (_session is { } session)
        {
            await session.DisposeAsync();
        }

        await _loop.ConfigureAwait(false);
        Volatile.Write(ref _window, 0);
        _stopping.Dispose();
    }

    private async Task ReadAsync(
        InputProbeSession session,
        int cursor,
        CancellationToken cancellationToken
    )
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            IReadOnlyList<ProbeEvent> events;
            try
            {
                events = await session
                    .WaitForAsync(
                        cursor,
                        received => received.Count > 0,
                        WaitSlice,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                continue;
            }
            catch (Exception ex)
                when (ex
                        is InputProbeException
                            or OperationCanceledException
                            or ObjectDisposedException
                )
            {
                break;
            }

            cursor += events.Count;
            foreach (var probeEvent in events)
            {
                _measurements.OnProbe(ProbeSignals.Classify(probeEvent));
            }
        }

        Volatile.Write(ref _window, 0);
        _measurements.Log.Add("probe", "Sonda cerrada.");
        _measurements.PostChanged();
    }
}
