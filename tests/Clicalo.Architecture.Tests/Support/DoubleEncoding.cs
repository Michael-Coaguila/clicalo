using System.Text;

namespace Clicalo.Architecture.Tests.Support;

/// <summary>
/// Finds UTF-8 text that was decoded as Windows-1252 and saved again as UTF-8 (mojibake: the section sign becomes
/// U+00C2 U+00A7, «ñ» becomes U+00C3 U+00B1, an arrow becomes U+00E2 U+2020 U+2019). Windows PowerShell 5.1 does
/// exactly that when it reads a UTF-8 file without a byte order mark and writes it back.
/// </summary>
/// <remarks>
/// Only the lead bytes of the characters this repository writes are looked for: 0xC2 and 0xC3 (Latin-1: «§», «¡», «ñ»,
/// «ú», guillemets), 0xE2 (punctuation, arrows and operators: «→», «≤», «…», «—») and 0xEF (a byte order mark read as
/// text). Other lead bytes are ordinary letters in Spanish text: «Ñ» (0xD1) followed by a closing guillemet (0xBB) is
/// legitimate, not a double-encoded Cyrillic letter.
/// </remarks>
internal static class DoubleEncoding
{
    /// <summary>The characters Windows-1252 maps to the bytes 0x80–0x9F; the rest of its upper half is Latin-1.</summary>
    private static readonly Dictionary<char, byte> Windows1252Specials = new()
    {
        ['€'] = 0x80,
        ['‚'] = 0x82,
        ['ƒ'] = 0x83,
        ['„'] = 0x84,
        ['…'] = 0x85,
        ['†'] = 0x86,
        ['‡'] = 0x87,
        ['ˆ'] = 0x88,
        ['‰'] = 0x89,
        ['Š'] = 0x8A,
        ['‹'] = 0x8B,
        ['Œ'] = 0x8C,
        ['Ž'] = 0x8E,
        ['‘'] = 0x91,
        ['’'] = 0x92,
        ['“'] = 0x93,
        ['”'] = 0x94,
        ['•'] = 0x95,
        ['–'] = 0x96,
        ['—'] = 0x97,
        ['˜'] = 0x98,
        ['™'] = 0x99,
        ['š'] = 0x9A,
        ['›'] = 0x9B,
        ['œ'] = 0x9C,
        ['ž'] = 0x9E,
        ['Ÿ'] = 0x9F,
    };

    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true
    );

    /// <summary>
    /// Every double-encoded character of <paramref name="line"/>: a lead byte of the remarks followed by exactly the
    /// continuation bytes (0x80–0xBF) it needs, all written as their Windows-1252 characters, with what it meant.
    /// </summary>
    /// <param name="line">One line of a text file.</param>
    public static IEnumerable<(string Found, string Meant)> Find(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        for (var i = 0; i < line.Length; i++)
        {
            if (!TryByte(line[i], out var lead) || lead is not (0xC2 or 0xC3 or 0xE2 or 0xEF))
            {
                continue;
            }

            var length = lead < 0xE0 ? 2 : 3;
            if (i + length > line.Length)
            {
                continue;
            }

            var bytes = new byte[length];
            bytes[0] = lead;
            var complete = true;
            for (var k = 1; k < length && complete; k++)
            {
                complete = TryByte(line[i + k], out bytes[k]) && bytes[k] is >= 0x80 and <= 0xBF;
            }

            if (complete && TryDecode(bytes, out var meant))
            {
                yield return (line.Substring(i, length), meant);
                i += length - 1;
            }
        }
    }

    /// <summary>Whether <paramref name="bytes"/> is valid UTF-8.</summary>
    /// <param name="bytes">The content of a file.</param>
    public static bool IsValidUtf8(byte[] bytes) => TryDecode(bytes, out _);

    private static bool TryByte(char character, out byte value)
    {
        if (character is >= '\u0080' and <= 'ÿ')
        {
            value = (byte)character;
            return true;
        }

        return Windows1252Specials.TryGetValue(character, out value);
    }

    private static bool TryDecode(byte[] bytes, out string text)
    {
        try
        {
            text = StrictUtf8.GetString(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            text = string.Empty;
            return false;
        }
    }
}
