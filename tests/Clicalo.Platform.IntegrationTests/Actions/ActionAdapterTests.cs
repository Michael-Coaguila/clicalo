using System.Buffers.Binary;
using System.Text;
using Clicalo.Platform.Windows.Clipboard;
using Clicalo.Platform.Windows.Feedback;

namespace Clicalo.Platform.IntegrationTests.Actions;

/// <summary>The pure parts of the paste (EJE-008) and of the soft sound (EJE-012). Headless and deterministic.</summary>
public sealed class ActionAdapterTests
{
    [Theory]
    [Trait("Req", "EJE-008")]
    [InlineData(1u, true)] // CF_TEXT
    [InlineData(13u, true)] // CF_UNICODETEXT
    [InlineData(8u, true)] // CF_DIB
    [InlineData(15u, true)] // CF_HDROP
    [InlineData(0xC100u, true)] // a registered format such as HTML Format
    [InlineData(2u, false)] // CF_BITMAP
    [InlineData(14u, false)] // CF_ENHMETAFILE
    [InlineData(3u, false)] // CF_METAFILEPICT
    [InlineData(0x300u, false)] // CF_GDIOBJFIRST
    public void Only_formats_held_in_memory_are_captured(uint format, bool captured) =>
        ClipboardPaster.IsMemoryFormat(format).ShouldBe(captured);

    [Fact]
    [Trait("Req", "EJE-012")]
    public void The_click_is_a_short_quiet_pcm_wave()
    {
        var wave = FeedbackSound.Build();

        Encoding.ASCII.GetString(wave, 0, 4).ShouldBe("RIFF");
        Encoding.ASCII.GetString(wave, 8, 8).ShouldBe("WAVEfmt ");
        var rate = BinaryPrimitives.ReadInt32LittleEndian(wave.AsSpan(24));
        var data = BinaryPrimitives.ReadInt32LittleEndian(wave.AsSpan(40));
        (data / 2.0 / rate).ShouldBeLessThan(0.1);
        var loudest = 0;
        for (var i = 44; i < wave.Length; i += 2)
        {
            loudest = Math.Max(
                loudest,
                Math.Abs((int)BinaryPrimitives.ReadInt16LittleEndian(wave.AsSpan(i)))
            );
        }

        loudest.ShouldBeGreaterThan(0);
        loudest.ShouldBeLessThan(short.MaxValue / 3);
    }
}
