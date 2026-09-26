using System.IO.Pipes;
using System.Text;
using System.Threading.Channels;

namespace Clicalo.Tools.InputProbe;

/// <summary>
/// Client end of the duplex pipe. Events are queued by the window thread and written by a background loop, so a
/// slow reader never blocks message handling; commands are read by another loop and handed to the window.
/// </summary>
/// <remarks>
/// The handle is opened overlapped (<see cref="PipeOptions.Asynchronous"/>): with a synchronous handle Windows
/// serializes I/O per file object and a pending read would block every write.
/// </remarks>
internal sealed class ProbePipe : IDisposable
{
    private readonly NamedPipeClientStream _stream;
    private readonly Channel<byte[]> _outgoing = Channel.CreateUnbounded<byte[]>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = true }
    );
    private readonly CancellationTokenSource _stop = new();
    private Task _writeLoop = Task.CompletedTask;
    private Task _readLoop = Task.CompletedTask;

    private ProbePipe(NamedPipeClientStream stream)
    {
        _stream = stream;
    }

    /// <summary>Connects to <c>\\.\pipe\<paramref name="name"/></c>; returns null when no server answers in time.</summary>
    public static ProbePipe? TryConnect(string name, TimeSpan timeout)
    {
        var stream = new NamedPipeClientStream(
            ".",
            name,
            PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly
        );
        try
        {
            stream.Connect(timeout);
            return new ProbePipe(stream);
        }
        catch (Exception ex)
            when (ex is TimeoutException or IOException or UnauthorizedAccessException)
        {
            stream.Dispose();
            return null;
        }
    }

    /// <summary>
    /// Starts the write loop and the command loop. <paramref name="onLine"/> and <paramref name="onClosed"/> run on
    /// thread-pool threads; <paramref name="onClosed"/> runs once the client closes its end or the pipe breaks.
    /// </summary>
    public void Start(Action<string> onLine, Action onClosed)
    {
        _writeLoop = Task.Run(() => WriteLoopAsync(onClosed));
        _readLoop = Task.Run(() => ReadLoopAsync(onLine, onClosed));
    }

    /// <summary>Queues one complete line (UTF-8 JSON plus <c>\n</c>). Never blocks.</summary>
    public void Send(byte[] line) => _outgoing.Writer.TryWrite(line);

    /// <summary>Stops accepting lines and waits up to <paramref name="drainTimeout"/> for the queued ones.</summary>
    public void Complete(TimeSpan drainTimeout)
    {
        _outgoing.Writer.TryComplete();
        _ = _writeLoop.Wait(drainTimeout);
    }

    public void Dispose()
    {
        _outgoing.Writer.TryComplete();
        _stop.Cancel();
        _stream.Dispose();
        _ = Task.WhenAll(_writeLoop, _readLoop).Wait(TimeSpan.FromSeconds(1));
        _stop.Dispose();
    }

    private async Task WriteLoopAsync(Action onClosed)
    {
        try
        {
            await foreach (var line in _outgoing.Reader.ReadAllAsync(_stop.Token))
            {
                await _stream.WriteAsync(line, _stop.Token);
            }
        }
        catch (IOException)
        {
            onClosed();
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
        {
            // Shutting down.
        }
    }

    private async Task ReadLoopAsync(Action<string> onLine, Action onClosed)
    {
        try
        {
            using var reader = new StreamReader(
                _stream,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 1024,
                leaveOpen: true
            );
            while (await reader.ReadLineAsync(_stop.Token) is { } line)
            {
                onLine(line);
            }
        }
        catch (Exception ex)
            when (ex is IOException or OperationCanceledException or ObjectDisposedException)
        {
            // The client went away or we are shutting down: either way the probe must close.
        }

        onClosed();
    }
}
