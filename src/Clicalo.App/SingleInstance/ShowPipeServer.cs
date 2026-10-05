using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using Clicalo.Domain.Timing;
using Clicalo.Platform.Windows.SingleInstance;
using Microsoft.Extensions.Logging;

namespace Clicalo.App.SingleInstance;

/// <summary>
/// The server of the single-instance pipe <c>\\.\pipe\Clicalo.{sidHash}.{sessionId}</c> (blueprint §3.4, ADR-0010):
/// <list type="bullet">
/// <item>DACL <c>(A;;GRGW;;;UserSid)(D;;GA;;;NU)</c>: only the user, never over the network;</item>
/// <item>the first instance is created with <c>FILE_FLAG_FIRST_PIPE_INSTANCE</c>, so a process that took the name
/// first is detected (<c>ipc.squat_detected</c>) instead of served;</item>
/// <item><c>Timings.Ipc.IpcMaxInstances</c> instances, messages of at most <c>IpcMaxMessageBytes</c>, each request
/// within <c>IpcRequestTimeout</c> and at most <c>IpcRateLimit</c> per window;</item>
/// <item>the client must be in this session and run as this user, and its integrity is read
/// (<see cref="PipeAdmission"/>: the server's half of the two-way check);</item>
/// <item>the only verb is <c>show</c> (D13: nothing that injects, edits or takes the foreground). In M2 every admitted
/// client may only show, so a client of lower integrity gets no more than the ADR allows it.</item>
/// </list>
/// <see cref="ShowRequested"/> is raised on a thread-pool thread; the composition marshals it to the Surfaces role.
/// </summary>
internal sealed partial class ShowPipeServer : IAsyncDisposable, IDisposable
{
    private readonly InstanceIdentity _identity;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _stop = new();
    private readonly Queue<long> _recent = new();
    private readonly Lock _rateGate = new();
    private readonly List<Task> _listeners = [];
    private readonly uint? _integrity = ProcessIdentity.IntegrityLevel((uint)Environment.ProcessId);
    private int _answered;
    private int _disposed;

    /// <summary>Creates the server; <see cref="Start"/> opens the pipe.</summary>
    public ShowPipeServer(
        InstanceIdentity identity,
        TimeProvider time,
        ILogger<ShowPipeServer> logger
    )
    {
        _identity = identity;
        _time = time;
        _logger = logger;
    }

    /// <summary>A client asked this instance to show the panel (SIS-003).</summary>
    public event EventHandler? ShowRequested;

    /// <summary>Whether the pipe could not be created because another process holds its name.</summary>
    public bool IsSquatted { get; private set; }

    /// <summary>Requests answered so far (for tests and diagnostics).</summary>
    public int Answered => Volatile.Read(ref _answered);

    /// <summary>
    /// Creates the first instance (failing over to <see cref="IsSquatted"/> when the name is taken) and starts the
    /// listeners.
    /// </summary>
    public void Start()
    {
        NamedPipeServerStream first;
        try
        {
            first = Create(firstInstance: true);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            IsSquatted = true;
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                LogSquatted(_logger, ex.GetType().Name);
            }

            return;
        }

        _listeners.Add(Task.Run(() => ListenAsync(first, _stop.Token)));
        for (var i = 1; i < Timings.Ipc.IpcMaxInstances; i++)
        {
            _listeners.Add(Task.Run(() => ListenAsync(null, _stop.Token)));
        }
    }

    /// <summary>Closes every instance and waits for the listeners.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await _stop.CancelAsync().ConfigureAwait(false);
        try
        {
            await Task.WhenAll(_listeners).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Stopping.
        }

        _stop.Dispose();
    }

    /// <summary>Closes every instance without waiting for the listeners (the process is ending).</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _stop.Cancel();
        _stop.Dispose();
    }

    private NamedPipeServerStream Create(bool firstInstance)
    {
        var security = new PipeSecurity();
        security.AddAccessRule(
            new PipeAccessRule(
                _identity.UserSid,
                PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
                AccessControlType.Allow
            )
        );
        security.AddAccessRule(
            new PipeAccessRule(
                new SecurityIdentifier(WellKnownSidType.NetworkSid, null),
                PipeAccessRights.FullControl,
                AccessControlType.Deny
            )
        );
        var options = PipeOptions.Asynchronous;
        if (firstInstance)
        {
            options |= PipeOptions.FirstPipeInstance;
        }

        return NamedPipeServerStreamAcl.Create(
            _identity.PipeName,
            PipeDirection.InOut,
            Timings.Ipc.IpcMaxInstances,
            PipeTransmissionMode.Message,
            options,
            (int)Timings.Ipc.IpcMaxMessageBytes,
            (int)Timings.Ipc.IpcMaxMessageBytes,
            security
        );
    }

    private async Task ListenAsync(
        NamedPipeServerStream? first,
        CancellationToken cancellationToken
    )
    {
        var pipe = first;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (pipe is null)
                {
                    try
                    {
                        pipe = Create(firstInstance: false);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        // No instance slot (someone else holds instances of this name): try again later, never spin.
                        LogFailure(ex);
                        await Task.Delay(Timings.Ipc.IpcRequestTimeout, _time, cancellationToken)
                            .ConfigureAwait(false);
                        continue;
                    }
                }

                await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                await ServeAsync(pipe, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (OperationCanceledException)
            {
                // The request took longer than Timings.Ipc.IpcRequestTimeout: dropped, the listener goes on.
                LogRequestFailed(_logger, nameof(TimeoutException));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A client that vanished before its answer: the listener goes on.
                LogFailure(ex);
            }
            finally
            {
                if (pipe is not null)
                {
                    await pipe.DisposeAsync().ConfigureAwait(false);
                    pipe = null;
                }
            }
        }
    }

    private async Task ServeAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(Timings.Ipc.IpcRequestTimeout, _time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeout.Token
        );
        var status = Admit(pipe);
        if (status == PipeStatus.Ok)
        {
            var request = await ReadMessageAsync(pipe, linked.Token).ConfigureAwait(false);
            status = request is null ? PipeStatus.Rejected : PipeProtocol.ReadRequest(request);
        }

        await pipe.WriteAsync(PipeProtocol.Response(status), linked.Token).ConfigureAwait(false);
        await pipe.FlushAsync(linked.Token).ConfigureAwait(false);
        _ = Interlocked.Increment(ref _answered);
        LogAnswered(_logger, status);
        if (status == PipeStatus.Ok)
        {
            ShowRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>The client must be in this session and run as this user, and within the rate limit.</summary>
    private PipeStatus Admit(NamedPipeServerStream pipe)
    {
        var client = PipeClient.Of(pipe.SafePipeHandle);
        var trust = PipeAdmission.Of(client, _identity, _integrity);
        LogClient(_logger, trust, client.IntegrityLevel ?? 0);
        if (trust == ClientTrust.Rejected)
        {
            return PipeStatus.Rejected;
        }

        var limit = Timings.Ipc.IpcRateLimit;
        var now = _time.GetTimestamp();
        lock (_rateGate)
        {
            while (_recent.Count > 0 && _time.GetElapsedTime(_recent.Peek(), now) >= limit.Window)
            {
                _ = _recent.Dequeue();
            }

            if (_recent.Count >= limit.Count)
            {
                return PipeStatus.Rejected;
            }

            _recent.Enqueue(now);
        }

        return PipeStatus.Ok;
    }

    private static async Task<byte[]?> ReadMessageAsync(
        NamedPipeServerStream pipe,
        CancellationToken cancellationToken
    )
    {
        var buffer = new byte[Timings.Ipc.IpcMaxMessageBytes];
        var total = 0;
        do
        {
            var read = await pipe.ReadAsync(buffer.AsMemory(total), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                return null;
            }

            total += read;
            if (!pipe.IsMessageComplete && total >= buffer.Length)
            {
                // Larger than the limit: refused without reading the rest.
                return null;
            }
        } while (!pipe.IsMessageComplete);

        return buffer[..total];
    }

    private void LogFailure(Exception exception)
    {
        var failure = exception.GetType().Name;
        LogRequestFailed(_logger, failure);
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "ipc.squat_detected: the single-instance pipe name is taken ({Exception})"
    )]
    private static partial void LogSquatted(ILogger logger, string exception);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "ipc.request answered {Status}")]
    private static partial void LogAnswered(ILogger logger, PipeStatus status);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Debug,
        Message = "ipc.request failed ({Exception})"
    )]
    private static partial void LogRequestFailed(ILogger logger, string exception);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Debug,
        Message = "ipc.client {Trust}, integrity 0x{Integrity:X}"
    )]
    private static partial void LogClient(ILogger logger, ClientTrust trust, uint integrity);
}
