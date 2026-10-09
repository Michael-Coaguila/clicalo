using System.Buffers.Binary;
using Clicalo.Application.Ports;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Media.Audio;

namespace Clicalo.Platform.Windows.Feedback;

/// <summary>
/// The soft sound after an action (EJE-012, GEN-011): «a short click confirms the shortcut was sent». A 40 ms tone of
/// 1 kHz that fades out, at a quarter of the full volume, built once in memory (no file, no resource) and played with
/// <c>PlaySound(SND_MEMORY | SND_ASYNC)</c> from the thread pool, so the engine never waits for the audio stack.
/// </summary>
public sealed class FeedbackSound : IFeedbackSound
{
    private const int SampleRate = 22_050;
    private const int Frequency = 1_000;
    private const double Seconds = 0.04;
    private const double Volume = 0.25;

    // Pinned for the life of the process: PlaySound reads it while the sound plays (SND_ASYNC).
    private static readonly byte[] Wave = Build();

    /// <inheritdoc />
    public void Play() => _ = ThreadPool.UnsafeQueueUserWorkItem(static _ => PlayNow(), null);

    /// <summary>The click as a 16-bit mono PCM WAV file.</summary>
    internal static byte[] Build()
    {
        var samples = (int)(SampleRate * Seconds);
        var data = samples * sizeof(short);
        var wave = GC.AllocateArray<byte>(44 + data, pinned: true);
        var span = wave.AsSpan();
        "RIFF"u8.CopyTo(span);
        BinaryPrimitives.WriteInt32LittleEndian(span[4..], 36 + data);
        "WAVEfmt "u8.CopyTo(span[8..]);
        BinaryPrimitives.WriteInt32LittleEndian(span[16..], 16);
        BinaryPrimitives.WriteInt16LittleEndian(span[20..], 1); // PCM
        BinaryPrimitives.WriteInt16LittleEndian(span[22..], 1); // mono
        BinaryPrimitives.WriteInt32LittleEndian(span[24..], SampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(span[28..], SampleRate * sizeof(short));
        BinaryPrimitives.WriteInt16LittleEndian(span[32..], sizeof(short));
        BinaryPrimitives.WriteInt16LittleEndian(span[34..], 16);
        "data"u8.CopyTo(span[36..]);
        BinaryPrimitives.WriteInt32LittleEndian(span[40..], data);
        for (var i = 0; i < samples; i++)
        {
            var t = (double)i / SampleRate;
            var fade = Math.Exp(-t / (Seconds / 4));
            var value = Math.Sin(2 * Math.PI * Frequency * t) * fade * Volume * short.MaxValue;
            BinaryPrimitives.WriteInt16LittleEndian(
                span[(44 + (i * sizeof(short)))..],
                (short)value
            );
        }

        return wave;
    }

    private static unsafe void PlayNow()
    {
        fixed (byte* sound = Wave)
        {
            _ = PInvoke.PlaySound(
                (PCWSTR)(char*)sound,
                HMODULE.Null,
                SND_FLAGS.SND_MEMORY | SND_FLAGS.SND_ASYNC | SND_FLAGS.SND_NODEFAULT
            );
        }
    }
}
