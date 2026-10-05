using Windows.Win32;
using Windows.Win32.System.StationsAndDesktops;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Clicalo.Platform.Core.Injection;

/// <summary>
/// The real <see cref="IKeyStateReader"/>: <c>OpenInputDesktop</c>, <c>GetAsyncKeyState</c> and <c>MapVirtualKey</c>.
/// It keeps no state, so any thread may use it.
/// </summary>
public sealed class SystemKeyState : IKeyStateReader
{
    private SystemKeyState() { }

    /// <summary>The only instance.</summary>
    public static SystemKeyState Instance { get; } = new();

    /// <inheritdoc />
    public bool CanRead
    {
        get
        {
            var desktop = PInvoke.OpenInputDesktop(
                default(DESKTOP_CONTROL_FLAGS),
                false,
                DESKTOP_ACCESS_FLAGS.DESKTOP_SWITCHDESKTOP
            );
            if (desktop.IsNull)
            {
                return false;
            }

            _ = PInvoke.CloseDesktop(desktop);
            return true;
        }
    }

    /// <inheritdoc />
    public bool IsDown(byte virtualKey) => PInvoke.GetAsyncKeyState(virtualKey) < 0;

    /// <inheritdoc />
    public ushort ScanCode(byte virtualKey) =>
        (ushort)PInvoke.MapVirtualKey(virtualKey, MAP_VIRTUAL_KEY_TYPE.MAPVK_VK_TO_VSC_EX);
}
