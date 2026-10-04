using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.Input.Pointer;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Clicalo.TestKit.Windows.Input;

/// <summary>
/// Test-only pointer injection (blueprint §10.1 <c>SyntheticPointer</c>): a finger or a pen through
/// <c>CreateSyntheticPointerDevice</c> and <c>InjectSyntheticPointerInput</c>, or a mouse through <c>SendInput</c>.
/// It only ever touches windows of the allowed processes (the test process and InputProbe) and refuses to inject
/// anything anywhere else.
/// </summary>
/// <remarks>
/// Safety rules, checked immediately before EVERY injected frame, never only once per gesture:
/// <list type="number">
/// <item>desktop tests are enabled (<see cref="DesktopTestEnvironment.IsEnabled"/>) and both the window under the
/// exact point (<c>WindowFromPoint</c>) and its root (<c>GetAncestor(GA_ROOT)</c>) belong to one of
/// <see cref="AllowedProcessIds"/>; otherwise <see cref="InjectionRefusedException"/> and nothing is injected;</item>
/// <item>every gesture is one call that always ends its contact (up, or cancel from a <c>finally</c>) and releases
/// the mouse buttons it pressed, so no contact or button can stay down; a pen also leaves the detection range. The
/// releases are the only frames sent after a failed check: a contact that went down belongs to the window it went down
/// on until it goes up;</item>
/// <item>no keyboard input is ever injected by this class.</item>
/// </list>
/// A finger waits <see cref="PenTouchSettle.Window"/> after a pen left the detection range, since Windows drops a touch
/// that comes sooner (pen and touch arbitration).
/// The check cannot see whether the window under the point is on screen yet: Windows routes a contact to what the
/// desktop window manager composed, so a contact sent right after a window was shown passes the check and falls through
/// to the window below, whatever its process. Fixtures wait for <see cref="Rendering.FirstFrame"/> before the first
/// gesture on a window they show. Every frame is traced for failure messages (<see cref="PointerFrameTrace"/>).
/// Coordinates are physical screen pixels: every gesture runs its thread per-monitor DPI aware. The mouse gestures put
/// the cursor back where it was, and mouse events carry <see cref="ExtraInfoMarker"/> in <c>dwExtraInfo</c>. Desktop
/// tests run it only with <c>CLICALO_DESKTOP_TESTS=1</c>; the hosted CI runners run them systematically.
/// </remarks>
public sealed class SyntheticPointer : IDisposable
{
    /// <summary><c>dwExtraInfo</c> of every injected mouse event ("CLP1").</summary>
    public const long ExtraInfoMarker = 0x434C_5031;

    private const double NormalizedMax = 65535;
    private const uint TouchPressure = 512;
    private const uint TouchOrientation = 90;
    private const int TouchContactRadius = 2;
    private const uint PenPressure = 512;

    private readonly Lock _gate = new();
    private HSYNTHETICPOINTERDEVICE _device;
    private bool _disposed;
    private string _lastTarget = "not checked";

    /// <summary>Creates an injector of <paramref name="kind"/> that may only touch windows of <paramref name="allowedProcessIds"/>.</summary>
    public SyntheticPointer(SyntheticPointerKind kind, IReadOnlyCollection<int> allowedProcessIds)
    {
        ArgumentNullException.ThrowIfNull(allowedProcessIds);
        if (allowedProcessIds.Count == 0)
        {
            throw new ArgumentException(
                "At least one allowed process is required.",
                nameof(allowedProcessIds)
            );
        }

        Kind = kind;
        AllowedProcessIds = allowedProcessIds;
    }

    /// <summary>Time between two frames of a gesture.</summary>
    public static TimeSpan FrameInterval { get; } = TimeSpan.FromMilliseconds(16);

    /// <summary>The impersonated device.</summary>
    public SyntheticPointerKind Kind { get; }

    /// <summary>The only processes whose windows may receive the input.</summary>
    public IReadOnlyCollection<int> AllowedProcessIds { get; }

    /// <summary>Gestures that went down so far (each one also went up or was cancelled).</summary>
    public int Gestures { get; private set; }

    /// <summary>
    /// True when the root window under (<paramref name="x"/>, <paramref name="y"/>) belongs to an allowed process;
    /// <paramref name="description"/> names the window's process for failure messages (never its title).
    /// </summary>
    public bool IsAllowedTarget(int x, int y, out string description)
    {
        using var dpi = ThreadDpiScope.PerMonitorV2();
        return IsAllowedTarget(new Point(x, y), AllowedProcessIds, out description);
    }

    /// <summary>
    /// Creates the synthetic finger or pen device now instead of on the first gesture (nothing is injected; a mouse
    /// has no device). Windows announces a new device to the windows of the desktop (<c>WM_TABLET_ADDED</c>) and
    /// delivers the first contact of a device only after that: a device created inside a measured gesture adds
    /// 15–200 ms (CI) that a touch screen, present since the session started, never adds. Latency measurements
    /// connect first and wait for the announcement.
    /// </summary>
    public void Connect()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (Kind != SyntheticPointerKind.Mouse)
            {
                _ = Device();
            }
        }
    }

    /// <summary>Down and up at a physical screen point, in one guarded gesture.</summary>
    public void Tap(int x, int y) => Gesture(new Point(x, y), new Point(x, y), TimeSpan.Zero);

    /// <summary>
    /// Down at a physical screen point, updates for <paramref name="duration"/> without moving, then up (a long press
    /// or a hold). The up is sent even if a check fails in between.
    /// </summary>
    public void Hold(int x, int y, TimeSpan duration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero);
        Gesture(new Point(x, y), new Point(x, y), duration);
    }

    /// <summary>A straight move from one point to another over <paramref name="duration"/>, down to up.</summary>
    public void Drag(int fromX, int fromY, int toX, int toY, TimeSpan duration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero);
        Gesture(new Point(fromX, fromY), new Point(toX, toY), duration);
    }

    /// <summary>Destroys the synthetic device.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_device != default)
            {
                PInvoke.DestroySyntheticPointerDevice(_device);
                PointerFrameTrace.Add(
                    Stopwatch.GetTimestamp(),
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{Kind} device 0x{Raw(_device):X} destroyed"
                    )
                );
                _device = default;
            }
        }
    }

    private static bool IsAllowedTarget(
        Point point,
        IReadOnlyCollection<int> allowed,
        out string description
    )
    {
        var hit = PInvoke.WindowFromPoint(point);
        if (hit.IsNull)
        {
            description = "no window";
            return false;
        }

        var root = PInvoke.GetAncestor(hit, GET_ANCESTOR_FLAGS.GA_ROOT);
        if (root.IsNull)
        {
            root = hit;
        }

        // Both the window that receives the input and its root must be allowed: a child window of another process
        // can live inside an allowed top-level window.
        var processId = ProcessOf(root);
        var hitProcessId = ProcessOf(hit);
        if (hitProcessId != processId && !allowed.Contains((int)hitProcessId))
        {
            description = string.Create(
                CultureInfo.InvariantCulture,
                $"child window 0x{(nint)hit:X} of {ProcessName(hitProcessId)} (pid {hitProcessId}) inside window 0x{(nint)root:X}"
            );
            return false;
        }

        description = string.Create(
            CultureInfo.InvariantCulture,
            $"window 0x{(nint)root:X} of {ProcessName(processId)} (pid {processId})"
        );
        return allowed.Contains((int)processId);
    }

    private static unsafe uint ProcessOf(HWND window)
    {
        uint processId;
        _ = PInvoke.GetWindowThreadProcessId(window, &processId);
        return processId;
    }

    private static string ProcessName(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return "an exited process";
        }
    }

    private static unsafe nint Raw(HSYNTHETICPOINTERDEVICE device) => (nint)device.Value;

    private static Point Between(Point from, Point to, double fraction) =>
        new(
            from.X + (int)Math.Round((to.X - from.X) * fraction, MidpointRounding.AwayFromZero),
            from.Y + (int)Math.Round((to.Y - from.Y) * fraction, MidpointRounding.AwayFromZero)
        );

    private static bool SamePoint(Point a, Point b) => a.X == b.X && a.Y == b.Y;

    /// <summary>Throws without injecting when the frame at <paramref name="point"/> may not be sent.</summary>
    private void EnsureAllowed(Point point, bool contactDown)
    {
        var consequence = contactDown
            ? "The contact was ended (up or cancel) and nothing else was injected."
            : "Nothing was injected.";
        if (!DesktopTestEnvironment.IsEnabled)
        {
            throw new InjectionRefusedException(
                "Pointer input is only injected in desktop tests ("
                    + DesktopTestEnvironment.Variable
                    + "=1). "
                    + consequence
            );
        }

        var allowed = IsAllowedTarget(point, AllowedProcessIds, out var description);
        _lastTarget = description;
        if (!allowed)
        {
            throw new InjectionRefusedException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The window under ({point.X}, {point.Y}) is {description}, which does not belong to an allowed process. "
                ) + consequence
            );
        }
    }

    private void Gesture(Point from, Point to, TimeSpan duration)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            using var dpi = ThreadDpiScope.PerMonitorV2();
            if (Kind == SyntheticPointerKind.Mouse)
            {
                MouseGesture(from, to, duration);
            }
            else
            {
                ContactGesture(from, to, duration);
            }
        }
    }

    // ---- Finger and pen: a synthetic pointer device -------------------------------------------------------------

    private void ContactGesture(Point from, Point to, TimeSpan duration)
    {
        var pen = Kind == SyntheticPointerKind.Pen;
        if (!pen)
        {
            // Windows drops a touch that comes right after a pen left (PenTouchSettle); waited before the check, so
            // the check stays immediately before the frame.
            var settle = PenTouchSettle.Shared.RemainingBeforeTouch(Stopwatch.GetTimestamp());
            if (settle > TimeSpan.Zero)
            {
                Thread.Sleep(settle);
            }
        }

        EnsureAllowed(from, contactDown: false);
        var last = from;
        if (pen)
        {
            // A pen comes into range, hovering, before it touches.
            InjectContact(from, ContactFrame.Hover);
        }

        try
        {
            if (pen)
            {
                EnsureAllowed(from, contactDown: false);
            }

            InjectContact(from, ContactFrame.Down);
            Gestures++;
            MoveAndLift(from, to, duration, ref last);
        }
        finally
        {
            if (pen)
            {
                // A lifted pen hovers; this frame takes it out of range, so it cannot stay near a window.
                try
                {
                    InjectContact(last, ContactFrame.Leave);
                }
                finally
                {
                    PenTouchSettle.Shared.PenLeft(Stopwatch.GetTimestamp());
                }
            }
        }
    }

    /// <summary>Moves the contact that is down at <paramref name="from"/> and lifts it, or cancels it on failure.</summary>
    private void MoveAndLift(Point from, Point to, TimeSpan duration, ref Point last)
    {
        var released = false;
        try
        {
            var started = Stopwatch.GetTimestamp();
            while (true)
            {
                Thread.Sleep(FrameInterval);
                var elapsed = Stopwatch.GetElapsedTime(started);
                if (elapsed >= duration)
                {
                    break;
                }

                var next = Between(from, to, elapsed / duration);
                EnsureAllowed(next, contactDown: true);
                InjectContact(next, ContactFrame.Update);
                last = next;
            }

            if (!SamePoint(last, to))
            {
                EnsureAllowed(to, contactDown: true);
                InjectContact(to, ContactFrame.Update);
                last = to;
                Thread.Sleep(FrameInterval);
            }

            EnsureAllowed(last, contactDown: true);
            InjectContact(last, ContactFrame.Up);
            released = true;
        }
        finally
        {
            if (!released)
            {
                InjectContact(last, ContactFrame.Cancel);
            }
        }
    }

    private unsafe void InjectContact(Point point, ContactFrame frame)
    {
        var info =
            Kind == SyntheticPointerKind.Pen ? PenFrame(point, frame) : TouchFrame(point, frame);
        var device = Device();
        var injected = PInvoke.InjectSyntheticPointerInput(device, &info, 1);
        PointerFrameTrace.Add(
            Stopwatch.GetTimestamp(),
            string.Create(
                CultureInfo.InvariantCulture,
                $"{Kind} {frame} at ({point.X}, {point.Y}) on device 0x{Raw(device):X}: {(injected ? "injected" : "refused")}; checked target {_lastTarget}; foreground 0x{(nint)PInvoke.GetForegroundWindow():X}"
            )
        );
        if (!injected)
        {
            var error = Marshal.GetLastPInvokeError();
            throw new InjectionRefusedException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"InjectSyntheticPointerInput refused the {frame} frame of a {Kind} at ({point.X}, {point.Y}) (Win32 error {error}). Input is blocked when the target runs at a higher integrity level (UIPI) or the desktop is locked."
                )
            );
        }
    }

    private HSYNTHETICPOINTERDEVICE Device()
    {
        if (_device == default)
        {
            _device = PInvoke.CreateSyntheticPointerDevice(
                Kind == SyntheticPointerKind.Pen
                    ? POINTER_INPUT_TYPE.PT_PEN
                    : POINTER_INPUT_TYPE.PT_TOUCH,
                1,
                POINTER_FEEDBACK_MODE.POINTER_FEEDBACK_NONE
            );
            PointerFrameTrace.Add(
                Stopwatch.GetTimestamp(),
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{Kind} device 0x{Raw(_device):X} created"
                )
            );
            if (_device == default)
            {
                throw new InjectionRefusedException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"CreateSyntheticPointerDevice failed (Win32 error {Marshal.GetLastPInvokeError()}); it needs Windows 10 1809 or later."
                    )
                );
            }
        }

        return _device;
    }

    private static POINTER_FLAGS Flags(ContactFrame frame, bool pen) =>
        frame switch
        {
            ContactFrame.Down => POINTER_FLAGS.POINTER_FLAG_DOWN
                | POINTER_FLAGS.POINTER_FLAG_INRANGE
                | POINTER_FLAGS.POINTER_FLAG_INCONTACT,
            ContactFrame.Update => POINTER_FLAGS.POINTER_FLAG_UPDATE
                | POINTER_FLAGS.POINTER_FLAG_INRANGE
                | POINTER_FLAGS.POINTER_FLAG_INCONTACT,
            ContactFrame.Up => pen
                ? POINTER_FLAGS.POINTER_FLAG_UP | POINTER_FLAGS.POINTER_FLAG_INRANGE
                : POINTER_FLAGS.POINTER_FLAG_UP,
            ContactFrame.Cancel => pen
                ? POINTER_FLAGS.POINTER_FLAG_UP
                    | POINTER_FLAGS.POINTER_FLAG_CANCELED
                    | POINTER_FLAGS.POINTER_FLAG_INRANGE
                : POINTER_FLAGS.POINTER_FLAG_UP | POINTER_FLAGS.POINTER_FLAG_CANCELED,
            ContactFrame.Hover => POINTER_FLAGS.POINTER_FLAG_UPDATE
                | POINTER_FLAGS.POINTER_FLAG_INRANGE,
            _ => POINTER_FLAGS.POINTER_FLAG_UPDATE,
        };

    private static POINTER_TYPE_INFO TouchFrame(Point point, ContactFrame frame)
    {
        var info = new POINTER_TYPE_INFO { type = POINTER_INPUT_TYPE.PT_TOUCH };
        info.Anonymous.touchInfo = new POINTER_TOUCH_INFO
        {
            pointerInfo = new POINTER_INFO
            {
                pointerType = POINTER_INPUT_TYPE.PT_TOUCH,
                pointerId = 0,
                ptPixelLocation = point,
                pointerFlags = Flags(frame, pen: false),
            },
            touchMask =
                PInvoke.TOUCH_MASK_CONTACTAREA
                | PInvoke.TOUCH_MASK_ORIENTATION
                | PInvoke.TOUCH_MASK_PRESSURE,
            rcContact = new RECT(
                point.X - TouchContactRadius,
                point.Y - TouchContactRadius,
                point.X + TouchContactRadius,
                point.Y + TouchContactRadius
            ),
            orientation = TouchOrientation,
            pressure = TouchPressure,
        };
        return info;
    }

    private static POINTER_TYPE_INFO PenFrame(Point point, ContactFrame frame)
    {
        var inContact = frame is ContactFrame.Down or ContactFrame.Update;
        var info = new POINTER_TYPE_INFO { type = POINTER_INPUT_TYPE.PT_PEN };
        info.Anonymous.penInfo = new POINTER_PEN_INFO
        {
            pointerInfo = new POINTER_INFO
            {
                pointerType = POINTER_INPUT_TYPE.PT_PEN,
                pointerId = 0,
                ptPixelLocation = point,
                pointerFlags = Flags(frame, pen: true),
            },
            penMask = PInvoke.PEN_MASK_PRESSURE,
            pressure = inContact ? PenPressure : 0,
        };
        return info;
    }

    // ---- Mouse: SendInput with absolute coordinates on the virtual desktop --------------------------------------

    private void MouseGesture(Point from, Point to, TimeSpan duration)
    {
        EnsureAllowed(from, contactDown: false);
        _ = PInvoke.GetCursorPos(out var original);
        try
        {
            if (duration == TimeSpan.Zero && SamePoint(from, to))
            {
                // A click is one atomic batch: move, press and release.
                SendMouse([
                    Move(from),
                    Button(MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTDOWN),
                    Button(MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTUP),
                ]);
                Gestures++;
                return;
            }

            SendMouse([Move(from), Button(MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTDOWN)]);
            Gestures++;
            MouseMoveAndRelease(from, to, duration);
        }
        finally
        {
            _ = PInvoke.SetCursorPos(original.X, original.Y);
        }
    }

    private void MouseMoveAndRelease(Point from, Point to, TimeSpan duration)
    {
        var released = false;
        try
        {
            var started = Stopwatch.GetTimestamp();
            var last = from;
            while (true)
            {
                Thread.Sleep(FrameInterval);
                var elapsed = Stopwatch.GetElapsedTime(started);
                if (elapsed >= duration)
                {
                    break;
                }

                var next = Between(from, to, elapsed / duration);
                if (!SamePoint(next, last))
                {
                    EnsureAllowed(next, contactDown: true);
                    SendMouse([Move(next)]);
                    last = next;
                }
            }

            EnsureAllowed(to, contactDown: true);
            SendMouse([Move(to), Button(MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTUP)]);
            released = true;
        }
        finally
        {
            if (!released)
            {
                SendMouse([Button(MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTUP)]);
            }
        }
    }

    private static INPUT Move(Point point)
    {
        var left = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_XVIRTUALSCREEN);
        var top = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_YVIRTUALSCREEN);
        var width = Math.Max(2, PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXVIRTUALSCREEN));
        var height = Math.Max(2, PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYVIRTUALSCREEN));
        var input = new INPUT { type = INPUT_TYPE.INPUT_MOUSE };
        input.Anonymous.mi = new MOUSEINPUT
        {
            dx = Normalize(point.X - left, width),
            dy = Normalize(point.Y - top, height),
            dwFlags =
                MOUSE_EVENT_FLAGS.MOUSEEVENTF_MOVE
                | MOUSE_EVENT_FLAGS.MOUSEEVENTF_ABSOLUTE
                | MOUSE_EVENT_FLAGS.MOUSEEVENTF_VIRTUALDESK,
            dwExtraInfo = (nuint)ExtraInfoMarker,
        };
        return input;
    }

    private static int Normalize(int offset, int extent) =>
        (int)Math.Round(offset * NormalizedMax / (extent - 1), MidpointRounding.AwayFromZero);

    private static INPUT Button(MOUSE_EVENT_FLAGS flags)
    {
        var input = new INPUT { type = INPUT_TYPE.INPUT_MOUSE };
        input.Anonymous.mi = new MOUSEINPUT
        {
            dwFlags = flags,
            dwExtraInfo = (nuint)ExtraInfoMarker,
        };
        return input;
    }

    private void SendMouse(INPUT[] inputs)
    {
        uint inserted;
        unsafe
        {
            inserted = PInvoke.SendInput(inputs, sizeof(INPUT));
        }

        PointerFrameTrace.Add(
            Stopwatch.GetTimestamp(),
            string.Create(
                CultureInfo.InvariantCulture,
                $"Mouse {string.Join("+", inputs.Select(input => input.Anonymous.mi.dwFlags))}: {inserted} of {inputs.Length} inserted; checked target {_lastTarget}"
            )
        );

        if (inserted != inputs.Length)
        {
            throw new InjectionRefusedException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"SendInput inserted {inserted} of {inputs.Length} mouse events (Win32 error {Marshal.GetLastPInvokeError()}). Input is blocked when the target runs at a higher integrity level (UIPI) or the desktop is locked."
                )
            );
        }
    }
}
