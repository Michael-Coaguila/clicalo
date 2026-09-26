using System.Collections.Immutable;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Keys;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Clicalo.Platform.Windows.Input;

/// <summary>
/// Builds the pure layout table the engine resolves characters with (blueprint §7.7): for each character key of the
/// catalog (<c>char:ñ</c>…), <c>VkKeyScanEx</c> in the layout of the foreground thread gives the virtual key and the
/// Shift or AltGr it needs, and <c>MapVirtualKeyEx(VK → VSC_EX)</c> its scan code. A character the layout cannot type
/// is left out, so the engine warns instead of sending something else (EC-EJE-10). Captured on every foreground
/// change and on <c>WM_INPUTLANGCHANGE</c>.
/// </summary>
public static unsafe class KeyboardLayoutCapture
{
    private const int ShiftBit = 0x01;
    private const int CtrlBit = 0x02;
    private const int AltBit = 0x04;
    private const int AltGrBits = CtrlBit | AltBit;

    /// <summary>The layout of the thread that owns <paramref name="window"/>.</summary>
    /// <param name="window">A window handle.</param>
    public static KeyboardLayoutSnapshot ForWindow(nint window) =>
        Capture(
            (nint)
                PInvoke
                    .GetKeyboardLayout(PInvoke.GetWindowThreadProcessId(new HWND(window), null))
                    .Value
        );

    /// <summary>The layout of the foreground window's thread.</summary>
    public static KeyboardLayoutSnapshot ForForeground() =>
        ForWindow((nint)PInvoke.GetForegroundWindow().Value);

    /// <summary>The table of a layout (<c>HKL</c>).</summary>
    /// <param name="layout">The layout handle.</param>
    public static KeyboardLayoutSnapshot Capture(nint layout)
    {
        var hkl = new HKL(layout);
        var characters = ImmutableDictionary.CreateBuilder<KeyId, LayoutKey>();
        foreach (var definition in KeyDefinitions.All)
        {
            if (!definition.Id.IsCharacter)
            {
                continue;
            }

            var text = definition.Id.Value[KeyId.CharacterPrefix.Length..];
            if (text.Length != 1 || Resolve(text[0], hkl) is not { } key)
            {
                continue;
            }

            characters[definition.Id] = key;
        }

        return new KeyboardLayoutSnapshot(
            new KeyboardLayoutId((ulong)layout),
            characters.ToImmutable()
        );
    }

    private static LayoutKey? Resolve(char character, HKL layout)
    {
        var scan = PInvoke.VkKeyScanEx(character, layout);
        if (scan == -1)
        {
            return null;
        }

        var vk = (ushort)(scan & 0xFF);
        var state = (scan >> 8) & 0xFF;
        var needsAltGr = (state & AltGrBits) == AltGrBits;
        if (vk == 0 || state > (ShiftBit | AltGrBits) || ((state & AltGrBits) != 0 && !needsAltGr))
        {
            // Ctrl-only, Alt-only or Kana/OEM states cannot be sent as a stroke.
            return null;
        }

        var mapped = PInvoke.MapVirtualKeyEx(vk, MAP_VIRTUAL_KEY_TYPE.MAPVK_VK_TO_VSC_EX, layout);
        return new LayoutKey(
            vk,
            (ushort)(mapped & 0xFF),
            (mapped & 0xFF00) == 0xE000,
            NeedsShift: (state & ShiftBit) != 0,
            needsAltGr
        );
    }
}
