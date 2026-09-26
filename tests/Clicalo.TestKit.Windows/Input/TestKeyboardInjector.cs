using System.Globalization;
using System.Runtime.InteropServices;
using Clicalo.TestKit.Windows.Probe;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Clicalo.TestKit.Windows.Input;

/// <summary>
/// A minimal <c>SendInput</c> injector for tests, and only for tests: it sends keyboard batches to one target
/// window (an InputProbe) and refuses to send anything anywhere else. The product injector is
/// <c>SendInputInjector</c> in Platform.Core (milestone M2).
/// </summary>
/// <remarks>
/// Safety rules, all checked before a single event is sent:
/// <list type="number">
/// <item>the batch is balanced and small (<see cref="KeyStrokeBatch.Validate"/>), so it cannot leave a key down;</item>
/// <item>the target window exists and owns the foreground (<c>GetForegroundWindow() == target</c>);</item>
/// <item>no modifier is held (physically or by another program), so the batch cannot combine with it;</item>
/// <item>outside continuous integration, no right Ctrl and no right Alt (AltGr): the maintainer's dictation and voice
/// tools capture them (<see cref="DesktopTestEnvironment.IsContinuousIntegration"/>).</item>
/// </list>
/// The batch is then sent with one atomic <c>SendInput</c> call, and the foreground is checked again afterwards.
/// Every event carries <see cref="ExtraInfoMarker"/> in <c>dwExtraInfo</c>, so the probe's records can be filtered
/// to the input this injector produced.
/// </remarks>
public sealed class TestKeyboardInjector
{
    /// <summary><c>dwExtraInfo</c> of every injected event ("CLK1"); 32 bits, as Raw Input keeps only 32.</summary>
    public const long ExtraInfoMarker = 0x434C_4B31;

    private static readonly (VIRTUAL_KEY Key, string Name)[] Modifiers =
    [
        (VIRTUAL_KEY.VK_LSHIFT, "left Shift"),
        (VIRTUAL_KEY.VK_RSHIFT, "right Shift"),
        (VIRTUAL_KEY.VK_LCONTROL, "left Ctrl"),
        (VIRTUAL_KEY.VK_RCONTROL, "right Ctrl"),
        (VIRTUAL_KEY.VK_LMENU, "left Alt"),
        (VIRTUAL_KEY.VK_RMENU, "right Alt"),
        (VIRTUAL_KEY.VK_LWIN, "left Windows"),
        (VIRTUAL_KEY.VK_RWIN, "right Windows"),
    ];

    private const ushort CtrlScanCode = 0x1D;
    private const ushort AltScanCode = 0x38;

    private readonly Action<IReadOnlyList<KeyStroke>> _sendInput;
    private readonly Func<bool> _reservedKeysAllowed;
    private int _sentBatches;

    /// <summary>Creates an injector bound to <paramref name="targetWindow"/>.</summary>
    public TestKeyboardInjector(nint targetWindow)
        : this(targetWindow, SendInputBatch) { }

    /// <summary>
    /// Creates an injector whose final step is <paramref name="sendInput"/> instead of <c>SendInput</c>. Used by the
    /// injector's own tests, so that a broken guard can never type into the developer's foreground window.
    /// </summary>
    internal TestKeyboardInjector(nint targetWindow, Action<IReadOnlyList<KeyStroke>> sendInput)
        : this(targetWindow, sendInput, static () => DesktopTestEnvironment.IsContinuousIntegration)
    { }

    /// <summary>
    /// Creates an injector whose final step is <paramref name="sendInput"/> and that sends right Ctrl and right Alt only
    /// while <paramref name="reservedKeysAllowed"/> says so. Used by the injector's own tests.
    /// </summary>
    internal TestKeyboardInjector(
        nint targetWindow,
        Action<IReadOnlyList<KeyStroke>> sendInput,
        Func<bool> reservedKeysAllowed
    )
    {
        if (targetWindow == 0)
        {
            throw new ArgumentException("The target window cannot be null.", nameof(targetWindow));
        }

        TargetWindow = targetWindow;
        _sendInput = sendInput;
        _reservedKeysAllowed = reservedKeysAllowed;
    }

    /// <summary>Creates an injector bound to the window of <paramref name="probe"/>.</summary>
    public TestKeyboardInjector(InputProbeSession probe)
        : this((probe ?? throw new ArgumentNullException(nameof(probe))).Window) { }

    /// <summary>The only window this injector sends input to.</summary>
    public nint TargetWindow { get; }

    /// <summary>Number of batches that passed every check and were handed to <c>SendInput</c> so far.</summary>
    public int SentBatches => Volatile.Read(ref _sentBatches);

    /// <summary>
    /// The batch exactly as it would be sent now: virtual-key strokes without a scan code get the informative
    /// scan code and extended flag of the target's keyboard layout (<c>MapVirtualKeyEx</c>, <c>MAPVK_VK_TO_VSC_EX</c>),
    /// as the product does in normal mode (blueprint §7.7).
    /// </summary>
    public IReadOnlyList<KeyStroke> Resolve(IReadOnlyList<KeyStroke> batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        var layout = KeyboardLayouts.OfWindow(TargetWindow);
        var resolved = new KeyStroke[batch.Count];
        for (var i = 0; i < batch.Count; i++)
        {
            var stroke = batch[i];
            if (stroke.IsUnicode || stroke.IsScanCodeMode || stroke.ScanCode != 0)
            {
                resolved[i] = stroke;
                continue;
            }

            var (scanCode, extended) = KeyboardLayouts.ToScanCode(stroke.VirtualKey, layout);
            resolved[i] = stroke with
            {
                ScanCode = scanCode,
                Flags =
                    stroke.Flags
                    | (extended ? KeyStrokeOptions.ExtendedKey : KeyStrokeOptions.None),
            };
        }

        return resolved;
    }

    /// <summary>
    /// Validates <paramref name="batch"/>, checks the safety preconditions and sends it with one <c>SendInput</c>.
    /// Throws <see cref="ArgumentException"/> or <see cref="InjectionRefusedException"/> without injecting
    /// anything when a rule does not hold.
    /// </summary>
    public void Send(IReadOnlyList<KeyStroke> batch)
    {
        KeyStrokeBatch.Validate(batch);
        EnsureNoReservedKey(batch);
        EnsureTargetOwnsForeground();
        EnsureNoModifierHeld();

        var resolved = Resolve(batch);
        EnsureNoReservedKey(resolved);

        // Last check immediately before the call keeps the window for a foreground change as small as possible.
        EnsureTargetOwnsForeground();
        Interlocked.Increment(ref _sentBatches);
        _sendInput(resolved);

        if (!ForegroundWindows.IsForeground(TargetWindow))
        {
            throw new InjectionRefusedException(
                "The foreground changed while the batch was being sent, so part of it may have reached another window: "
                    + ForegroundWindows.Describe()
                    + ". The batch was balanced, so no key was left down."
            );
        }
    }

    /// <summary>Sends <paramref name="strokes"/> with one atomic <c>SendInput</c> call.</summary>
    private static void SendInputBatch(IReadOnlyList<KeyStroke> strokes)
    {
        var inputs = new INPUT[strokes.Count];
        for (var i = 0; i < strokes.Count; i++)
        {
            inputs[i] = ToInput(strokes[i]);
        }

        uint inserted;
        unsafe
        {
            inserted = PInvoke.SendInput(inputs, sizeof(INPUT));
        }

        if (inserted != inputs.Length)
        {
            var error = Marshal.GetLastPInvokeError();
            throw new InjectionRefusedException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"SendInput inserted {inserted} of {inputs.Length} events (Win32 error {error}). Input is blocked when the target runs at a higher integrity level (UIPI) or the desktop is locked."
                )
            );
        }
    }

    private static INPUT ToInput(KeyStroke stroke)
    {
        var input = new INPUT { type = INPUT_TYPE.INPUT_KEYBOARD };
        input.Anonymous.ki = new KEYBDINPUT
        {
            wVk =
                stroke.IsUnicode || stroke.IsScanCodeMode
                    ? default
                    : (VIRTUAL_KEY)stroke.VirtualKey,
            wScan = stroke.ScanCode,
            dwFlags = (KEYBD_EVENT_FLAGS)(uint)stroke.Flags,
            time = 0,
            dwExtraInfo = (nuint)ExtraInfoMarker,
        };
        return input;
    }

    /// <summary>
    /// Right Ctrl and right Alt (AltGr), by virtual key or by extended scan code: the maintainer's dictation tools
    /// (Wispr Flow, Typeless) and Voice access hook them, so they are only injected on a CI runner.
    /// </summary>
    private void EnsureNoReservedKey(IReadOnlyList<KeyStroke> resolved)
    {
        foreach (var stroke in resolved)
        {
            var reserved =
                !stroke.IsUnicode
                && (
                    stroke.VirtualKey is VirtualKeyCode.RightControl or VirtualKeyCode.RightMenu
                    || (stroke.IsExtended && stroke.ScanCode is CtrlScanCode or AltScanCode)
                );
            if (reserved && !_reservedKeysAllowed())
            {
                throw new InjectionRefusedException(
                    "The batch holds right Ctrl or right Alt (AltGr), which the dictation and voice tools of this "
                        + "machine capture: they are only injected in continuous integration (CI=true). Nothing was "
                        + "injected."
                );
            }
        }
    }

    private static void EnsureNoModifierHeld()
    {
        foreach (var (key, name) in Modifiers)
        {
            if ((PInvoke.GetAsyncKeyState((int)key) & 0x8000) != 0)
            {
                throw new InjectionRefusedException(
                    "The "
                        + name
                        + " key is held down (physically or by another program). Nothing was injected, so the batch cannot combine with it; release it and run the test again."
                );
            }
        }
    }

    private void EnsureTargetOwnsForeground()
    {
        if (!ForegroundWindows.Exists(TargetWindow))
        {
            throw new InjectionRefusedException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The target window 0x{TargetWindow:X} does not exist. Nothing was injected."
                )
            );
        }

        if (!ForegroundWindows.IsForeground(TargetWindow))
        {
            throw new InjectionRefusedException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The target window 0x{TargetWindow:X} is not in the foreground ({ForegroundWindows.Describe()}). Nothing was injected: test input is only ever sent to the probe."
                )
            );
        }
    }
}
