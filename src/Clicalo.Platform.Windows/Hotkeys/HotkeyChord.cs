using System.Globalization;
using System.Runtime.InteropServices;
using Clicalo.Domain.Keys;

namespace Clicalo.Platform.Windows.Hotkeys;

/// <summary>
/// A combination of the closed list of global shortcuts (BUR-005, <c>data/catalogs/global-hotkeys.json</c>) as
/// <c>RegisterHotKey</c> takes it: modifier flags and one virtual key. The list only uses Ctrl, Alt and Shift with the
/// space bar or a function key, so this is the whole mapping; a combination it cannot express is refused.
/// </summary>
/// <param name="Modifiers">The <c>MOD_*</c> flags, without <c>MOD_NOREPEAT</c>.</param>
/// <param name="VirtualKey">The virtual key of the last key.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct HotkeyChord(uint Modifiers, uint VirtualKey)
{
    /// <summary><c>MOD_ALT</c>.</summary>
    public const uint Alt = 0x0001;

    /// <summary><c>MOD_CONTROL</c>.</summary>
    public const uint Control = 0x0002;

    /// <summary><c>MOD_SHIFT</c>.</summary>
    public const uint Shift = 0x0004;

    private const uint VkSpace = 0x20;
    private const uint VkF1 = 0x70;
    private const int LastFunctionKey = 24;

    /// <summary>
    /// The registration of <paramref name="chord"/>: every key but the last is Ctrl, Alt or Shift, and the last is the
    /// space bar or F1 to F24. <see langword="null"/> for anything else (never the Windows key, BUR-005).
    /// </summary>
    /// <param name="chord">The combination, in press order.</param>
    public static HotkeyChord? From(KeyChord chord)
    {
        ArgumentNullException.ThrowIfNull(chord);
        var strokes = chord.Strokes;
        if (strokes.Count < 2)
        {
            return null;
        }

        uint modifiers = 0;
        for (var i = 0; i < strokes.Count - 1; i++)
        {
            var flag = strokes[i].Key.Value switch
            {
                "ctrl" => Control,
                "alt" => Alt,
                "shift" => Shift,
                _ => 0u,
            };
            if (flag == 0 || (modifiers & flag) != 0)
            {
                return null;
            }

            modifiers |= flag;
        }

        return VirtualKeyOf(strokes[^1].Key.Value) is { } key
            ? new HotkeyChord(modifiers, key)
            : null;
    }

    private static uint? VirtualKeyOf(string key)
    {
        if (string.Equals(key, "space", StringComparison.Ordinal))
        {
            return VkSpace;
        }

        return
            key.Length is 2 or 3
            && key[0] == 'f'
            && int.TryParse(
                key.AsSpan(1),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var number
            )
            && number is >= 1 and <= LastFunctionKey
            ? VkF1 + (uint)(number - 1)
            : null;
    }
}
