using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Formatting;

namespace Clicalo.Infrastructure.Logging;

/// <summary>
/// The log file (blueprint §9.4, LOG-001): the current file is always <c>clicalo.log</c>; at
/// <c>Timings.Logging.LogFileMaxBytes</c> it renames <c>clicalo.3.log → clicalo.4.log</c> … <c>clicalo.log →
/// clicalo.1.log</c> and starts a new one, so at most <c>LogFileCount</c> files exist. Every line is redacted when it is
/// written (the Windows user never reaches the disk) and flushed at once so a crash keeps its last lines. A log failure
/// never breaks the app: it goes to Serilog's self-log.
/// </summary>
public sealed class FixedNameRollingFileSink : ILogEventSink, IDisposable
{
    private readonly string _path;
    private readonly long _maxBytes;
    private readonly int _fileCount;
    private readonly ITextFormatter _formatter;
    private readonly UserPathRedactor _redactor;
    private readonly Lock _gate = new();
    private FileStream? _stream;
    private bool _disposed;

    /// <summary>Creates the sink.</summary>
    /// <param name="path">The current file, <c>…\logs\clicalo.log</c>.</param>
    /// <param name="maxBytes">Size at which it rotates.</param>
    /// <param name="fileCount">Files kept, the current one included.</param>
    /// <param name="formatter">Renders one event.</param>
    /// <param name="redactor">Removes the Windows user from the rendered text.</param>
    public FixedNameRollingFileSink(
        string path,
        long maxBytes,
        int fileCount,
        ITextFormatter formatter,
        UserPathRedactor redactor
    )
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxBytes, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(fileCount, 1);
        ArgumentNullException.ThrowIfNull(formatter);
        ArgumentNullException.ThrowIfNull(redactor);
        _path = path;
        _maxBytes = maxBytes;
        _fileCount = fileCount;
        _formatter = formatter;
        _redactor = redactor;
    }

    /// <summary>The file of rotation <paramref name="index"/>: 0 is <c>clicalo.log</c>, 1 is <c>clicalo.1.log</c>…</summary>
    /// <param name="path">The current file.</param>
    /// <param name="index">The rotation.</param>
    public static string RotationOf(string path, int index)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (index == 0)
        {
            return path;
        }

        var folder = Path.GetDirectoryName(path) ?? string.Empty;
        return Path.Combine(
            folder,
            Path.GetFileNameWithoutExtension(path)
                + "."
                + index.ToString(CultureInfo.InvariantCulture)
                + Path.GetExtension(path)
        );
    }

    /// <inheritdoc />
    public void Emit(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        using var text = new StringWriter(CultureInfo.InvariantCulture);
        _formatter.Format(logEvent, text);
        var bytes = Encoding.UTF8.GetBytes(_redactor.Redact(text.ToString()));
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                var stream = _stream ??= Open();
                if (stream.Length > 0 && stream.Length + bytes.Length > _maxBytes)
                {
                    Roll();
                    stream = _stream = Open();
                }

                stream.Write(bytes);
                stream.Flush(flushToDisk: false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                SelfLog.WriteLine("clicalo.log write failed: {0}", ex.GetType().Name);
                _stream?.Dispose();
                _stream = null;
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _stream?.Dispose();
            _stream = null;
        }
    }

    [SuppressMessage(
        "ApiDesign",
        "RS0030:Do not use banned APIs",
        Justification = "The rolling log keeps a fixed name and appends; it cannot go through the atomic document writer (RS0030 exception «log-sink»)."
    )]
    private FileStream Open()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? ".");
        return new FileStream(
            _path,
            new FileStreamOptions
            {
                Mode = FileMode.Append,
                Access = FileAccess.Write,
                Share = FileShare.Read | FileShare.Delete,
            }
        );
    }

    /// <summary>Shifts every file up one rotation, dropping the oldest.</summary>
    private void Roll()
    {
        _stream?.Dispose();
        _stream = null;
        var oldest = RotationOf(_path, _fileCount - 1);
        if (_fileCount == 1)
        {
            File.Delete(oldest);
            return;
        }

        File.Delete(oldest);
        for (var index = _fileCount - 2; index >= 0; index--)
        {
            var from = RotationOf(_path, index);
            if (File.Exists(from))
            {
                File.Move(from, RotationOf(_path, index + 1), overwrite: true);
            }
        }
    }
}
