namespace Clicalo.Tools.InputProbe.Protocol;

/// <summary>Values of the <see cref="ProbeFields.Kind"/> field, one per event type written by the probe.</summary>
internal static class ProbeEventKinds
{
    /// <summary>The window exists, Raw Input is registered and the probe accepts commands.</summary>
    public const string Ready = "ready";

    /// <summary><c>WM_KEYDOWN</c>, <c>WM_KEYUP</c>, <c>WM_SYSKEYDOWN</c> or <c>WM_SYSKEYUP</c>.</summary>
    public const string Key = "key";

    /// <summary><c>WM_CHAR</c>, <c>WM_SYSCHAR</c>, <c>WM_DEADCHAR</c> or <c>WM_SYSDEADCHAR</c>.</summary>
    public const string Char = "char";

    /// <summary><c>WM_UNICHAR</c> with a UTF-32 code point.</summary>
    public const string UniChar = "unichar";

    /// <summary><c>WM_INPUT</c> from a keyboard (Raw Input).</summary>
    public const string RawKey = "rawKey";

    /// <summary>A mouse button message (left, right, middle, X1 or X2; down, up or double click).</summary>
    public const string MouseButton = "mouseButton";

    /// <summary><c>WM_MOUSEWHEEL</c> or <c>WM_MOUSEHWHEEL</c>.</summary>
    public const string Wheel = "wheel";

    /// <summary><c>WM_ACTIVATE</c>.</summary>
    public const string Activate = "activate";

    /// <summary><c>WM_ACTIVATEAPP</c>.</summary>
    public const string ActivateApp = "activateApp";

    /// <summary><c>WM_SETFOCUS</c> or <c>WM_KILLFOCUS</c>.</summary>
    public const string Focus = "focus";

    /// <summary><c>WM_INPUTLANGCHANGE</c>.</summary>
    public const string InputLanguage = "inputLanguage";

    /// <summary>Any other recorded window message (IME, <c>WM_SYSCOMMAND</c>, <c>WM_NCACTIVATE</c>...).</summary>
    public const string Message = "message";

    /// <summary>Answer to <see cref="ProbeCommands.Ping"/>.</summary>
    public const string Pong = "pong";

    /// <summary>Answer to <see cref="ProbeCommands.Foreground"/>.</summary>
    public const string Foreground = "foreground";

    /// <summary>A failure inside the probe (bad command, Raw Input error...). The probe keeps running.</summary>
    public const string Error = "error";

    /// <summary>The window is being destroyed; nothing follows.</summary>
    public const string Exit = "exit";
}
