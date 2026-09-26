using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Clicalo.Tools.InputProbe.Protocol;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Clicalo.TestKit.Windows.Probe;

/// <summary>
/// A running InputProbe: launches the process with a private pipe, records every event it reports and brings its
/// window to the foreground by legitimate means only.
/// </summary>
/// <remarks>
/// SendInput is asynchronous (the events are queued to the raw input thread), so there is no exact barrier between
/// injecting and observing: wait for the expected events with <see cref="WaitForAsync"/>, or use
/// <see cref="CollectAsync"/>, which also waits a settle period to catch unexpected extra events.
/// </remarks>
public sealed class InputProbeSession : IAsyncDisposable
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly TimeSpan ForegroundRetryInterval = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(3);
    private const int MaxEventsInMessages = 60;

    private readonly Process _process;
    private readonly NamedPipeServerStream _pipe;
    private readonly StreamWriter _writer;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly CancellationTokenSource _stopping = new();
    private readonly Lock _gate = new();
    private readonly List<ProbeEvent> _events = [];
    private TaskCompletionSource _changed = NewSignal();
    private Task _readLoop = Task.CompletedTask;
    private Exception? _protocolFailure;
    private bool _closed;
    private long _nextCommandId;
    private int _disposed;
    private ProbeReadyEvent? _ready;

    private InputProbeSession(Process process, NamedPipeServerStream pipe)
    {
        _process = process;
        _pipe = pipe;
        _writer = new StreamWriter(pipe, Utf8NoBom, bufferSize: 1024, leaveOpen: true)
        {
            NewLine = "\n",
        };
    }

    /// <summary>Maximum time to start the probe and receive its ready event.</summary>
    public static TimeSpan StartTimeout { get; } = TimeSpan.FromSeconds(15);

    /// <summary>Extra time <see cref="CollectAsync"/> waits after the expected events, to catch unexpected ones.</summary>
    public static TimeSpan SettleTime { get; } = TimeSpan.FromMilliseconds(150);

    /// <summary>The ready event: window, process, session, keyboard layout and timestamp frequency.</summary>
    public ProbeReadyEvent Ready =>
        _ready ?? throw new InvalidOperationException("The probe has not reported ready.");

    /// <summary>The probe window: the only window a test may inject into.</summary>
    public nint Window => Ready.Window;

    /// <summary>Process identifier of the probe.</summary>
    public int ProcessId => _process.Id;

    /// <summary>True while the probe window owns the foreground.</summary>
    public bool IsForeground => ForegroundWindows.IsForeground(Window);

    /// <summary>The number of events received so far: pass it to the methods that read "events since".</summary>
    public int Cursor
    {
        get
        {
            lock (_gate)
            {
                return _events.Count;
            }
        }
    }

    /// <summary>Launches the probe and waits for it to report ready.</summary>
    public static async Task<InputProbeSession> StartAsync(
        CancellationToken cancellationToken = default
    )
    {
        var executable = InputProbeLocator.Resolve();
        var pipeName = "clicalo-inputprobe-" + Guid.NewGuid().ToString("N");
        var pipe = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly
        );
        Process? process = null;
        InputProbeSession? session = null;
        try
        {
            var startInfo = new ProcessStartInfo(executable)
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(executable)!,
            };
            startInfo.ArgumentList.Add(ProbeProtocol.PipeSwitch);
            startInfo.ArgumentList.Add(pipeName);
            process =
                Process.Start(startInfo)
                ?? throw new InputProbeException("Could not start " + executable + ".");

            // Granted before the window exists, so the probe's own SetForegroundWindow at start-up can succeed.
            // Windows honours it only if this process may itself set the foreground window.
            _ = PInvoke.AllowSetForegroundWindow((uint)process.Id);

            await WaitForConnectionAsync(pipe, process, cancellationToken);
            session = new InputProbeSession(process, pipe);
            session._readLoop = Task.Run(session.ReadLoopAsync, CancellationToken.None);
            var events = await session.WaitForAsync(
                0,
                received => received.OfType<ProbeReadyEvent>().Any(),
                StartTimeout,
                cancellationToken
            );
            var ready = events.OfType<ProbeReadyEvent>().First();
            if (ready.Protocol != ProbeProtocol.Version)
            {
                throw new InputProbeException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"InputProbe speaks protocol {ready.Protocol}; this TestKit expects {ProbeProtocol.Version}. Rebuild the solution."
                    )
                );
            }

            session._ready = ready;
            return session;
        }
        catch
        {
            if (session is not null)
            {
                await session.DisposeAsync();
            }
            else
            {
                Kill(process);
                process?.Dispose();
                await pipe.DisposeAsync();
            }

            throw;
        }
    }

    /// <summary>A copy of every event received so far.</summary>
    public IReadOnlyList<ProbeEvent> Events() => EventsSince(0);

    /// <summary>A copy of the events received after <paramref name="cursor"/> (see <see cref="Cursor"/>).</summary>
    public IReadOnlyList<ProbeEvent> EventsSince(int cursor)
    {
        lock (_gate)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(cursor, _events.Count);
            return _events[cursor..];
        }
    }

    /// <summary>
    /// Waits until the events received after <paramref name="cursor"/> satisfy <paramref name="isComplete"/> and
    /// returns them. Fails fast when the probe reports an error or exits, and with a dump of what it received on
    /// timeout.
    /// </summary>
    public async Task<IReadOnlyList<ProbeEvent>> WaitForAsync(
        int cursor,
        Func<IReadOnlyList<ProbeEvent>, bool> isComplete,
        TimeSpan timeout,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(isComplete);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        while (true)
        {
            Task changed;
            IReadOnlyList<ProbeEvent> received;
            bool closed;
            lock (_gate)
            {
                changed = _changed.Task;
                received = _events[cursor..];
                closed = _closed;
            }

            if (received.OfType<ProbeErrorEvent>().FirstOrDefault() is { } error)
            {
                throw new InputProbeException(
                    "InputProbe reported an error: " + error.Detail + Dump(received)
                );
            }

            if (isComplete(received))
            {
                return received;
            }

            if (closed)
            {
                throw new InputProbeException(
                    "InputProbe closed its pipe before the expected events arrived."
                        + Dump(received),
                    _protocolFailure ?? new EndOfStreamException()
                );
            }

            try
            {
                await changed.WaitAsync(deadline.Token);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"InputProbe did not report the expected events within {timeout.TotalMilliseconds} ms."
                    ) + Dump(received),
                    ex
                );
            }
        }
    }

    /// <summary>
    /// <see cref="WaitForAsync"/>, then waits <see cref="SettleTime"/> and returns every event received after
    /// <paramref name="cursor"/>, so assertions also see anything unexpected that followed.
    /// </summary>
    public async Task<IReadOnlyList<ProbeEvent>> CollectAsync(
        int cursor,
        Func<IReadOnlyList<ProbeEvent>, bool> isComplete,
        TimeSpan timeout,
        CancellationToken cancellationToken = default
    )
    {
        await WaitForAsync(cursor, isComplete, timeout, cancellationToken);
        await Task.Delay(SettleTime, cancellationToken);
        return EventsSince(cursor);
    }

    /// <summary>Round-trips a ping: every command sent before it has been handled by the probe.</summary>
    public async Task PingAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var cursor = Cursor;
        var id = await SendCommandAsync(ProbeCommands.Ping, cancellationToken);
        await WaitForAsync(
            cursor,
            received => received.OfType<ProbePongEvent>().Any(pong => pong.Id == id),
            timeout,
            cancellationToken
        );
    }

    /// <summary>
    /// Tries to give the probe the foreground with legitimate means only: <c>SetForegroundWindow</c> from this
    /// process and, after granting it <c>AllowSetForegroundWindow</c>, from the probe itself. No input is injected
    /// and no thread input is attached, so the outcome is exactly what Windows allows in this session (spike S0).
    /// </summary>
    /// <returns>True when the probe owns the foreground.</returns>
    public async Task<bool> TryBringToForegroundAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default
    )
    {
        var started = Stopwatch.GetTimestamp();
        while (true)
        {
            if (IsForeground)
            {
                return true;
            }

            _ = PInvoke.AllowSetForegroundWindow((uint)ProcessId);
            _ = PInvoke.SetForegroundWindow((HWND)Window);
            if (IsForeground)
            {
                return true;
            }

            var remaining = timeout - Stopwatch.GetElapsedTime(started);
            if (remaining <= TimeSpan.Zero)
            {
                return false;
            }

            var cursor = Cursor;
            var id = await SendCommandAsync(ProbeCommands.Foreground, cancellationToken);
            try
            {
                await WaitForAsync(
                    cursor,
                    received =>
                        received.OfType<ProbeForegroundEvent>().Any(answer => answer.Id == id),
                    remaining,
                    cancellationToken
                );
            }
            catch (TimeoutException)
            {
                return IsForeground;
            }

            if (IsForeground)
            {
                return true;
            }

            await Task.Delay(ForegroundRetryInterval, cancellationToken);
        }
    }

    /// <summary><see cref="TryBringToForegroundAsync"/>, failing with a diagnostic when Windows refuses.</summary>
    public async Task EnsureForegroundAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default
    )
    {
        if (!await TryBringToForegroundAsync(timeout, cancellationToken))
        {
            throw new InputProbeException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"InputProbe (window 0x{Window:X}) could not reach the foreground within {timeout.TotalMilliseconds} ms: "
                )
                    + ForegroundWindows.Describe()
                    + ". Windows only lets a process take the foreground in the situations documented for "
                    + "SetForegroundWindow; measuring whether a runner allows it is spike S0. No input was injected."
            );
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            if (!_process.HasExited)
            {
                using var quitTimeout = new CancellationTokenSource(ExitTimeout);
                await SendCommandAsync(ProbeCommands.Quit, quitTimeout.Token);
            }
        }
        catch (Exception ex)
            when (ex is IOException or ObjectDisposedException or OperationCanceledException)
        {
            // Already gone: the kill below covers the rest.
        }

        try
        {
            using var exitTimeout = new CancellationTokenSource(ExitTimeout);
            await _process.WaitForExitAsync(exitTimeout.Token);
        }
        catch (OperationCanceledException)
        {
            Kill(_process);
        }

        await _stopping.CancelAsync();
        try
        {
            await _writer.DisposeAsync();
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            // The pipe is already broken; nothing left to flush.
        }

        await _pipe.DisposeAsync();
        await _readLoop.ConfigureAwait(false);
        _process.Dispose();
        _writeGate.Dispose();
        _stopping.Dispose();
    }

    private static async Task WaitForConnectionAsync(
        NamedPipeServerStream pipe,
        Process process,
        CancellationToken cancellationToken
    )
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(StartTimeout);
        var connected = pipe.WaitForConnectionAsync(deadline.Token);
        var exited = process.WaitForExitAsync(deadline.Token);
        try
        {
            if (await Task.WhenAny(connected, exited) == exited && !connected.IsCompleted)
            {
                await exited;
                throw new InputProbeException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"InputProbe exited with code {process.ExitCode} before connecting to its pipe."
                    )
                );
            }

            await connected;
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InputProbeException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"InputProbe did not connect to its pipe within {StartTimeout.TotalSeconds} s."
                ),
                ex
            );
        }
    }

    private static void Kill(Process? process)
    {
        try
        {
            if (process is { HasExited: false })
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
            when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // It exited in between.
        }
    }

    private static TaskCompletionSource NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static string Dump(IReadOnlyList<ProbeEvent> events)
    {
        var builder = new StringBuilder();
        builder
            .AppendLine()
            .Append(CultureInfo.InvariantCulture, $"Received {events.Count} events:");
        foreach (var probeEvent in events.Take(MaxEventsInMessages))
        {
            builder.AppendLine().Append("  ").Append(probeEvent.Json);
        }

        if (events.Count > MaxEventsInMessages)
        {
            builder
                .AppendLine()
                .Append(
                    CultureInfo.InvariantCulture,
                    $"  ... {events.Count - MaxEventsInMessages} more"
                );
        }

        return builder.ToString();
    }

    private async Task<long> SendCommandAsync(string command, CancellationToken cancellationToken)
    {
        var id = Interlocked.Increment(ref _nextCommandId);
        var buffer = new ArrayBufferWriter<byte>(64);
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteString(ProbeCommands.CommandField, command);
            json.WriteNumber(ProbeFields.Id, id);
            json.WriteEndObject();
        }

        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            await _writer.WriteLineAsync(
                Utf8NoBom.GetString(buffer.WrittenSpan).AsMemory(),
                cancellationToken
            );
            await _writer.FlushAsync(cancellationToken);
        }
        finally
        {
            _writeGate.Release();
        }

        return id;
    }

    private async Task ReadLoopAsync()
    {
        try
        {
            using var reader = new StreamReader(
                _pipe,
                Utf8NoBom,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 4096,
                leaveOpen: true
            );
            while (await reader.ReadLineAsync(_stopping.Token) is { } line)
            {
                Publish(ProbeEventParser.Parse(line));
            }
        }
        catch (FormatException ex)
        {
            _protocolFailure = ex;
        }
        catch (Exception ex)
            when (ex is IOException or ObjectDisposedException or OperationCanceledException)
        {
            // The probe exited or the session is being disposed.
        }
        finally
        {
            TaskCompletionSource signal;
            lock (_gate)
            {
                _closed = true;
                signal = _changed;
            }

            signal.TrySetResult();
        }
    }

    private void Publish(ProbeEvent probeEvent)
    {
        TaskCompletionSource signal;
        lock (_gate)
        {
            _events.Add(probeEvent);
            signal = _changed;
            _changed = NewSignal();
        }

        signal.TrySetResult();
    }
}
