using System.Buffers;
using System.Diagnostics;
using System.Text.Json;
using Clicalo.Tools.InputProbe.Protocol;
using Windows.Win32;

namespace Clicalo.Tools.InputProbe;

/// <summary>
/// Serializes events as single-line JSON objects and hands them to the pipe. Owned by the window thread: every
/// event is written from there, so sequence numbers follow the order in which messages were handled.
/// </summary>
internal sealed class EventEmitter : IDisposable
{
    private readonly ProbePipe _pipe;
    private readonly ArrayBufferWriter<byte> _buffer = new(512);
    private readonly Utf8JsonWriter _writer;
    private long _sequence;

    public EventEmitter(ProbePipe pipe)
    {
        _pipe = pipe;
        _writer = new Utf8JsonWriter(_buffer, new JsonWriterOptions { Indented = false });
    }

    /// <summary>
    /// Starts an event with the fields common to all of them (kind, sequence, QPC timestamp and foreground
    /// window) and returns the writer positioned inside the object. Finish it with <see cref="Commit"/>.
    /// </summary>
    public Utf8JsonWriter Begin(string kind)
    {
        _buffer.ResetWrittenCount();
        _writer.Reset(_buffer);
        _writer.WriteStartObject();
        _writer.WriteString(ProbeFields.Kind, kind);
        _writer.WriteNumber(ProbeFields.Sequence, ++_sequence);
        _writer.WriteNumber(ProbeFields.Timestamp, Stopwatch.GetTimestamp());
        _writer.WriteNumber(
            ProbeFields.ForegroundWindow,
            (long)(nint)PInvoke.GetForegroundWindow()
        );
        return _writer;
    }

    /// <summary>Closes the current event and queues it as one line.</summary>
    public void Commit()
    {
        _writer.WriteEndObject();
        _writer.Flush();
        var line = new byte[_buffer.WrittenCount + 1];
        _buffer.WrittenSpan.CopyTo(line);
        line[^1] = (byte)'\n';
        _pipe.Send(line);
    }

    /// <summary>Emits an <see cref="ProbeEventKinds.Error"/> event. The detail never contains user content.</summary>
    public void Error(string detail)
    {
        var json = Begin(ProbeEventKinds.Error);
        json.WriteString(ProbeFields.Detail, detail);
        Commit();
    }

    public void Dispose() => _writer.Dispose();
}
