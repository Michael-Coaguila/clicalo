using Windows.Win32;

namespace Clicalo.Tools.InputProbe;

/// <summary>Symbolic names of the window messages the probe records, for readable logs and failure messages.</summary>
internal static class MessageNames
{
    /// <summary>The <c>WM_*</c> name of <paramref name="message"/>, or its hexadecimal value when not recorded.</summary>
    public static string Of(uint message) =>
        message switch
        {
            PInvoke.WM_KEYDOWN => "WM_KEYDOWN",
            PInvoke.WM_KEYUP => "WM_KEYUP",
            PInvoke.WM_SYSKEYDOWN => "WM_SYSKEYDOWN",
            PInvoke.WM_SYSKEYUP => "WM_SYSKEYUP",
            PInvoke.WM_CHAR => "WM_CHAR",
            PInvoke.WM_SYSCHAR => "WM_SYSCHAR",
            PInvoke.WM_DEADCHAR => "WM_DEADCHAR",
            PInvoke.WM_SYSDEADCHAR => "WM_SYSDEADCHAR",
            PInvoke.WM_UNICHAR => "WM_UNICHAR",
            PInvoke.WM_INPUT => "WM_INPUT",
            PInvoke.WM_LBUTTONDOWN => "WM_LBUTTONDOWN",
            PInvoke.WM_LBUTTONUP => "WM_LBUTTONUP",
            PInvoke.WM_LBUTTONDBLCLK => "WM_LBUTTONDBLCLK",
            PInvoke.WM_RBUTTONDOWN => "WM_RBUTTONDOWN",
            PInvoke.WM_RBUTTONUP => "WM_RBUTTONUP",
            PInvoke.WM_RBUTTONDBLCLK => "WM_RBUTTONDBLCLK",
            PInvoke.WM_MBUTTONDOWN => "WM_MBUTTONDOWN",
            PInvoke.WM_MBUTTONUP => "WM_MBUTTONUP",
            PInvoke.WM_MBUTTONDBLCLK => "WM_MBUTTONDBLCLK",
            PInvoke.WM_XBUTTONDOWN => "WM_XBUTTONDOWN",
            PInvoke.WM_XBUTTONUP => "WM_XBUTTONUP",
            PInvoke.WM_XBUTTONDBLCLK => "WM_XBUTTONDBLCLK",
            PInvoke.WM_MOUSEWHEEL => "WM_MOUSEWHEEL",
            PInvoke.WM_MOUSEHWHEEL => "WM_MOUSEHWHEEL",
            PInvoke.WM_ACTIVATE => "WM_ACTIVATE",
            PInvoke.WM_ACTIVATEAPP => "WM_ACTIVATEAPP",
            PInvoke.WM_NCACTIVATE => "WM_NCACTIVATE",
            PInvoke.WM_MOUSEACTIVATE => "WM_MOUSEACTIVATE",
            PInvoke.WM_SETFOCUS => "WM_SETFOCUS",
            PInvoke.WM_KILLFOCUS => "WM_KILLFOCUS",
            PInvoke.WM_INPUTLANGCHANGE => "WM_INPUTLANGCHANGE",
            PInvoke.WM_IME_SETCONTEXT => "WM_IME_SETCONTEXT",
            PInvoke.WM_IME_NOTIFY => "WM_IME_NOTIFY",
            PInvoke.WM_IME_STARTCOMPOSITION => "WM_IME_STARTCOMPOSITION",
            PInvoke.WM_IME_COMPOSITION => "WM_IME_COMPOSITION",
            PInvoke.WM_IME_ENDCOMPOSITION => "WM_IME_ENDCOMPOSITION",
            PInvoke.WM_IME_CHAR => "WM_IME_CHAR",
            PInvoke.WM_SYSCOMMAND => "WM_SYSCOMMAND",
            _ => "0x" + message.ToString("X4", System.Globalization.CultureInfo.InvariantCulture),
        };
}
