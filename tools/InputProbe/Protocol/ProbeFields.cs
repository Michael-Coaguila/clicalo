namespace Clicalo.Tools.InputProbe.Protocol;

/// <summary>JSON property names used by events and commands.</summary>
internal static class ProbeFields
{
    // ---- Common to every event -----------------------------------------------------------------------------

    /// <summary>Event type, one of <see cref="ProbeEventKinds"/>.</summary>
    public const string Kind = "kind";

    /// <summary>Sequence number, starting at 1 and without gaps.</summary>
    public const string Sequence = "seq";

    /// <summary>Monotonic QueryPerformanceCounter ticks taken when the probe handled the message.</summary>
    public const string Timestamp = "qpc";

    /// <summary><c>GetMessageTime</c> of the message (milliseconds since boot, wraps around).</summary>
    public const string MessageTime = "time";

    /// <summary><c>GetForegroundWindow</c> when the message was handled.</summary>
    public const string ForegroundWindow = "fg";

    /// <summary>
    /// The <c>dwExtraInfo</c> of injected input: <c>GetMessageExtraInfo</c>, or <c>RAWKEYBOARD.ExtraInformation</c>
    /// (32 bits) for Raw Input.
    /// </summary>
    public const string ExtraInfo = "extra";

    /// <summary>Window message number.</summary>
    public const string Message = "msg";

    /// <summary>Symbolic name of <see cref="Message"/> (for example <c>WM_KEYDOWN</c>).</summary>
    public const string MessageName = "name";

    /// <summary>Raw <c>wParam</c> of the message.</summary>
    public const string WParam = "wParam";

    /// <summary>Raw <c>lParam</c> of the message.</summary>
    public const string LParam = "lParam";

    // ---- Keyboard messages ---------------------------------------------------------------------------------

    /// <summary>Virtual-key code as delivered in <c>wParam</c> (generic for Shift, Ctrl and Alt).</summary>
    public const string VirtualKey = "vk";

    /// <summary>Side-specific virtual key derived from the scan code and the extended flag.</summary>
    public const string SideVirtualKey = "vkEx";

    /// <summary>Scan code: bits 16–23 of <c>lParam</c>, or the Raw Input make code.</summary>
    public const string ScanCode = "scan";

    /// <summary>Extended-key flag (bit 24 of <c>lParam</c>).</summary>
    public const string Extended = "ext";

    /// <summary>Repeat count (bits 0–15 of <c>lParam</c>).</summary>
    public const string RepeatCount = "repeat";

    /// <summary>Context code: Alt was down (bit 29 of <c>lParam</c>).</summary>
    public const string AltDown = "alt";

    /// <summary>Previous key state: the key was already down (bit 30 of <c>lParam</c>).</summary>
    public const string WasDown = "prev";

    /// <summary>Transition state: the key is being released (bit 31 of <c>lParam</c>).</summary>
    public const string Released = "up";

    /// <summary>Side-specific modifiers down according to <c>GetKeyState</c>, as <c>SideModifiers</c> bits.</summary>
    public const string Modifiers = "mods";

    /// <summary>UTF-16 code unit of a character message.</summary>
    public const string CodeUnit = "unit";

    /// <summary>Complete text of a character message; absent while half of a surrogate pair is pending.</summary>
    public const string Text = "text";

    /// <summary>UTF-32 code point of <c>WM_UNICHAR</c>.</summary>
    public const string CodePoint = "codePoint";

    // ---- Raw Input -----------------------------------------------------------------------------------------

    /// <summary><c>RAWKEYBOARD.Flags</c> (<c>RI_KEY_BREAK</c>, <c>RI_KEY_E0</c>, <c>RI_KEY_E1</c>).</summary>
    public const string RawFlags = "flags";

    /// <summary><c>RAWKEYBOARD.Message</c>.</summary>
    public const string RawMessage = "rawMsg";

    /// <summary><c>RAWINPUTHEADER.hDevice</c>: zero for injected input.</summary>
    public const string Device = "device";

    /// <summary>True when the input arrived while the window was not in the foreground (<c>RIM_INPUTSINK</c>).</summary>
    public const string Sink = "sink";

    // ---- Mouse ---------------------------------------------------------------------------------------------

    /// <summary>Mouse button: <c>left</c>, <c>right</c>, <c>middle</c>, <c>x1</c> or <c>x2</c>.</summary>
    public const string Button = "button";

    /// <summary>True for button-down and double-click messages.</summary>
    public const string Down = "down";

    /// <summary>True for double-click messages.</summary>
    public const string DoubleClick = "dbl";

    /// <summary>Horizontal coordinate: client area for buttons, screen for the wheel.</summary>
    public const string X = "x";

    /// <summary>Vertical coordinate: client area for buttons, screen for the wheel.</summary>
    public const string Y = "y";

    /// <summary><c>MK_*</c> key flags from the low word of <c>wParam</c>.</summary>
    public const string KeyFlags = "keys";

    /// <summary>Signed wheel delta (multiples of <c>WHEEL_DELTA</c> for detented wheels).</summary>
    public const string Delta = "delta";

    /// <summary>True for <c>WM_MOUSEHWHEEL</c>.</summary>
    public const string Horizontal = "horizontal";

    // ---- Activation, focus and language --------------------------------------------------------------------

    /// <summary><c>WM_ACTIVATE</c> state: 0 inactive, 1 active, 2 click-active.</summary>
    public const string State = "state";

    /// <summary><c>WM_ACTIVATE</c>: the window is minimized.</summary>
    public const string Minimized = "minimized";

    /// <summary>The other window of an activation or focus change (may be zero).</summary>
    public const string OtherWindow = "other";

    /// <summary><c>WM_ACTIVATEAPP</c>: true when the application is being activated.</summary>
    public const string Active = "active";

    /// <summary><c>WM_ACTIVATEAPP</c>: thread identifier of the other application.</summary>
    public const string OtherThread = "otherThread";

    /// <summary><c>WM_SETFOCUS</c> (true) or <c>WM_KILLFOCUS</c> (false).</summary>
    public const string Gained = "gained";

    /// <summary>Keyboard layout handle (<c>HKL</c>).</summary>
    public const string KeyboardLayout = "hkl";

    /// <summary><c>WM_INPUTLANGCHANGE</c> character set.</summary>
    public const string CharSet = "charset";

    // ---- Control events and commands -----------------------------------------------------------------------

    /// <summary>Protocol version (ready event).</summary>
    public const string Protocol = "protocol";

    /// <summary>A window handle: the probe window in the ready event, the target of the foreground command.</summary>
    public const string Window = "hwnd";

    /// <summary>Process identifier of the probe.</summary>
    public const string ProcessId = "pid";

    /// <summary>Thread identifier of the probe window.</summary>
    public const string ThreadId = "tid";

    /// <summary>Remote Desktop Services session of the probe (0 means no interactive desktop).</summary>
    public const string Session = "session";

    /// <summary><c>Stopwatch.Frequency</c>: QueryPerformanceCounter ticks per second.</summary>
    public const string TimestampFrequency = "qpcFrequency";

    /// <summary>Correlation identifier echoed by answers to commands.</summary>
    public const string Id = "id";

    /// <summary>Result of <c>SetForegroundWindow</c> in a foreground event.</summary>
    public const string Succeeded = "ok";

    /// <summary>Human-readable description of an error event (never user content).</summary>
    public const string Detail = "detail";
}
