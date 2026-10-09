using System.Security.Cryptography;
using Clicalo.Application.Ports;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Timing;
using Clicalo.Platform.Windows.SysEvents;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Memory;

namespace Clicalo.Platform.Windows.Clipboard;

/// <summary>
/// The paste of a Text action (EJE-008, blueprint §7.7) on the SysEvents thread, whose message window owns the
/// clipboard while Clícalo writes it:
/// <list type="number">
/// <item>captures every format of the clipboard that is memory (up to <c>Timings.Injection.ClipboardCaptureMaxBytes</c>;
/// bitmaps, metafiles and other GDI handles are left out, as their memory twins such as <c>CF_DIB</c> carry them);</item>
/// <item>puts the text with the formats that keep it out of the clipboard history, the cloud clipboard and clipboard
/// monitors;</item>
/// <item>tells the engine, which sends Ctrl+V (<see cref="EngineEvent.ClipboardReady"/>);</item>
/// <item>after <c>Timings.Injection.ClipboardRestoreDelay</c> puts the original back, only if the clipboard still holds
/// Clícalo's text (its sequence number did not change).</item>
/// </list>
/// A second paste before the restore keeps the first capture, so the user's clipboard is what comes back. When the
/// clipboard stays busy the paste does not happen and the engine sends nothing. The text is wiped from Clícalo's buffers.
/// </summary>
public sealed class ClipboardPaster : IClipboardPaster, IDisposable
{
    private const uint UnicodeText = 13;
    private const int OpenAttempts = 5;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(20);

    private readonly SysEventsThread _thread;
    private readonly TimeProvider _time;
    private readonly Lazy<uint[]> _privacyFormats = new(PrivacyFormats);
    private List<(uint Format, byte[] Data)>? _saved;
    private uint _ownSequence;
    private ITimer? _restore;
    private ITimer? _retry;
    private int _disposed;

    /// <summary>Creates the paster over <paramref name="thread"/>.</summary>
    /// <param name="thread">The SysEvents thread, whose message window owns the clipboard.</param>
    /// <param name="time">Times the restore.</param>
    public ClipboardPaster(SysEventsThread thread, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(thread);
        ArgumentNullException.ThrowIfNull(time);
        _thread = thread;
        _time = time;
    }

    /// <inheritdoc />
    public void Prepare(EffectId effect, ReadOnlySpan<char> text, IEngineInbox replyTo)
    {
        ArgumentNullException.ThrowIfNull(replyTo);
        var copy = text.ToArray();
        try
        {
            _thread.Post(() => Paste(effect, copy, replyTo, attempt: 1));
        }
        catch (ObjectDisposedException)
        {
            Array.Clear(copy);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _restore?.Dispose();
            _retry?.Dispose();
        }
    }

    /// <summary>The Unicode text on the clipboard, or null (tests; on the SysEvents thread).</summary>
    internal unsafe string? ReadText()
    {
        if (!PInvoke.OpenClipboard((HWND)_thread.MessageWindow))
        {
            return null;
        }

        try
        {
            var handle = (HGLOBAL)(nint)PInvoke.GetClipboardData(UnicodeText).Value;
            if (handle.IsNull)
            {
                return null;
            }

            var chars = (char*)PInvoke.GlobalLock(handle);
            try
            {
                return chars is null ? null : new string(chars);
            }
            finally
            {
                _ = PInvoke.GlobalUnlock(handle);
            }
        }
        finally
        {
            _ = PInvoke.CloseClipboard();
        }
    }

    /// <summary>Whether <paramref name="format"/> is held in global memory and can be copied and put back.</summary>
    /// <param name="format">A clipboard format.</param>
    internal static bool IsMemoryFormat(uint format) =>
        format
            is not (
                    2 // CF_BITMAP
                    or 3 // CF_METAFILEPICT
                    or 9 // CF_PALETTE
                    or 14 // CF_ENHMETAFILE
                    or 0x80 // CF_OWNERDISPLAY
                    or 0x82 // CF_DSPBITMAP
                    or 0x83 // CF_DSPMETAFILEPICT
                    or 0x8E // CF_DSPENHMETAFILE
                )
                and not (>= 0x300 and <= 0x3FF); // CF_GDIOBJFIRST to CF_GDIOBJLAST

    private void Paste(EffectId effect, char[] text, IEngineInbox replyTo, int attempt)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            Array.Clear(text);
            return;
        }

        if (!PInvoke.OpenClipboard((HWND)_thread.MessageWindow))
        {
            if (attempt < OpenAttempts)
            {
                _retry?.Dispose();
                _retry = Later(RetryDelay, () => Paste(effect, text, replyTo, attempt + 1));
            }
            else
            {
                Array.Clear(text);
            }

            return;
        }

        var bytes = Terminated(text);
        try
        {
            // A paste while the previous one waits to restore keeps the user's capture, not Clícalo's own text.
            _saved ??= Capture();
            if (!PInvoke.EmptyClipboard() || !Put(UnicodeText, bytes))
            {
                return;
            }

            foreach (var format in _privacyFormats.Value)
            {
                _ = Put(format, BitConverter.GetBytes(0));
            }
        }
        finally
        {
            _ = PInvoke.CloseClipboard();
            Array.Clear(text);
            CryptographicOperations.ZeroMemory(bytes);
        }

        _ownSequence = PInvoke.GetClipboardSequenceNumber();
        _ = replyTo.Post(new EngineEvent.ClipboardReady(effect));
        _restore?.Dispose();
        _restore = Later(Timings.Injection.ClipboardRestoreDelay, () => Restore(attempt: 1));
    }

    private void Restore(int attempt)
    {
        if (_saved is not { } saved)
        {
            return;
        }

        if (PInvoke.GetClipboardSequenceNumber() != _ownSequence)
        {
            // Someone copied something else meanwhile: theirs wins.
            Forget();
            return;
        }

        if (!PInvoke.OpenClipboard((HWND)_thread.MessageWindow))
        {
            if (attempt < OpenAttempts)
            {
                _restore = Later(RetryDelay, () => Restore(attempt + 1));
            }
            else
            {
                Forget();
            }

            return;
        }

        try
        {
            if (PInvoke.EmptyClipboard())
            {
                foreach (var (format, data) in saved)
                {
                    _ = Put(format, data);
                }
            }
        }
        finally
        {
            _ = PInvoke.CloseClipboard();
            Forget();
        }
    }

    private void Forget()
    {
        if (_saved is { } saved)
        {
            foreach (var (_, data) in saved)
            {
                CryptographicOperations.ZeroMemory(data);
            }
        }

        _saved = null;
    }

    private static unsafe List<(uint Format, byte[] Data)> Capture()
    {
        var saved = new List<(uint, byte[])>();
        long total = 0;
        for (
            var format = PInvoke.EnumClipboardFormats(0);
            format != 0;
            format = PInvoke.EnumClipboardFormats(format)
        )
        {
            if (!IsMemoryFormat(format))
            {
                continue;
            }

            var handle = (HGLOBAL)(nint)PInvoke.GetClipboardData(format).Value;
            if (handle.IsNull)
            {
                continue;
            }

            var size = (long)PInvoke.GlobalSize(handle);
            if (size <= 0 || total + size > Timings.Injection.ClipboardCaptureMaxBytes)
            {
                continue;
            }

            var source = PInvoke.GlobalLock(handle);
            if (source is null)
            {
                continue;
            }

            try
            {
                var data = new byte[size];
                new ReadOnlySpan<byte>(source, (int)size).CopyTo(data);
                saved.Add((format, data));
                total += size;
            }
            finally
            {
                _ = PInvoke.GlobalUnlock(handle);
            }
        }

        return saved;
    }

    private static unsafe bool Put(uint format, ReadOnlySpan<byte> data)
    {
        var memory = PInvoke.GlobalAlloc(
            GLOBAL_ALLOC_FLAGS.GMEM_MOVEABLE,
            (nuint)Math.Max(1, data.Length)
        );
        if (memory.IsNull)
        {
            return false;
        }

        var target = PInvoke.GlobalLock(memory);
        if (target is null)
        {
            _ = PInvoke.GlobalFree(memory);
            return false;
        }

        data.CopyTo(new Span<byte>(target, data.Length));
        _ = PInvoke.GlobalUnlock(memory);
        if (PInvoke.SetClipboardData(format, (HANDLE)(nint)memory.Value).IsNull)
        {
            _ = PInvoke.GlobalFree(memory);
            return false;
        }

        // The system owns the memory now.
        return true;
    }

    private static byte[] Terminated(char[] text)
    {
        var bytes = new byte[(text.Length + 1) * sizeof(char)];
        System.Runtime.InteropServices.MemoryMarshal.AsBytes(text.AsSpan()).CopyTo(bytes);
        return bytes;
    }

    private static uint[] PrivacyFormats() =>
        [
            PInvoke.RegisterClipboardFormat("ExcludeClipboardContentFromMonitorProcessing"),
            PInvoke.RegisterClipboardFormat("CanIncludeInClipboardHistory"),
            PInvoke.RegisterClipboardFormat("CanUploadToCloudClipboard"),
        ];

    private ITimer Later(TimeSpan delay, Action work) =>
        _time.CreateTimer(
            _ =>
            {
                try
                {
                    _thread.Post(work);
                }
                catch (ObjectDisposedException)
                {
                    // The SysEvents loop has ended with the process.
                }
            },
            null,
            delay,
            Timeout.InfiniteTimeSpan
        );
}
