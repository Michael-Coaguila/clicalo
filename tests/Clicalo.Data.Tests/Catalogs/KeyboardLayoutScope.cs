using System.Globalization;
using System.Runtime.Versioning;

namespace Clicalo.Data.Tests.Catalogs;

/// <summary>
/// Gives a handle to a keyboard layout (KLID such as 00000409). Uses a layout already loaded in the session;
/// otherwise loads it without telling the shell and unloads it on dispose, so the test leaves no trace.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class KeyboardLayoutScope : IDisposable
{
    private readonly bool _loadedHere;

    private KeyboardLayoutScope(nint handle, bool loadedHere)
    {
        Handle = handle;
        _loadedHere = loadedHere;
    }

    public nint Handle { get; }

    public static KeyboardLayoutScope Acquire(string layoutId)
    {
        var layout =
            int.Parse(layoutId, NumberStyles.HexNumber, CultureInfo.InvariantCulture) & 0xFFFF;
        var count = NativeKeyboard.GetKeyboardLayoutList(0, null);
        var handles = new nint[count];
        _ = NativeKeyboard.GetKeyboardLayoutList(count, handles);

        // The high word of an HKL identifies the keyboard layout; the low word is the input language.
        foreach (var handle in handles)
        {
            if (((handle >> 16) & 0xFFFF) == layout)
            {
                return new KeyboardLayoutScope(handle, loadedHere: false);
            }
        }

        var loaded = NativeKeyboard.LoadKeyboardLayoutW(layoutId, NativeKeyboard.KlfNoTellShell);
        if (loaded == 0)
        {
            throw new InvalidOperationException(
                "Keyboard layout " + layoutId + " could not be loaded."
            );
        }

        return new KeyboardLayoutScope(loaded, loadedHere: true);
    }

    public void Dispose()
    {
        if (_loadedHere)
        {
            _ = NativeKeyboard.UnloadKeyboardLayout(Handle);
        }
    }
}
