using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Clicalo.Platform.Core.KeyLedger;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Clicalo.Platform.Core.Injection;

/// <summary>
/// The only <c>PInvoke.SendInput</c> of the product (blueprint §4.4, §7.7), reachable only through
/// <see cref="InjectionGate"/>. Fills <c>INPUT</c> per mode: VK with the informative scan code and
/// <c>KEYEVENTF_EXTENDEDKEY</c>; scan code mode with <c>wVk = 0</c> and <c>KEYEVENTF_SCANCODE</c>; Unicode for text.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>One <c>SendInput</c> call per batch, so the system never interleaves other input inside it.</item>
/// <item>A Unicode unit becomes a down and an up (<c>KEYEVENTF_UNICODE</c>); the result counts whole
/// <see cref="LowLevelInput"/> items, so the gate commits exactly what went.</item>
/// <item>A move is absolute over the virtual desktop (<c>MOVE | ABSOLUTE | VIRTUALDESK</c>), normalized to 0‥65535.</item>
/// <item>Every event carries <see cref="ExtraInfo"/> in <c>dwExtraInfo</c>, so Clícalo's own hooks and the tests'
/// probe recognise Clícalo's input.</item>
/// </list>
/// </remarks>
public sealed class LowLevelInjector : ILowLevelSender
{
    /// <summary>
    /// The menu mask key (<c>VK 0xE8</c>, unassigned) sent before releasing Alt or Win so neither the Start menu nor a
    /// menu bar opens.
    /// </summary>
    public const ushort MenuMaskVirtualKey = 0xE8;

    /// <summary><c>dwExtraInfo</c> of every event the product injects ("CLKP"); 32 bits, as Raw Input keeps only 32.</summary>
    public const uint ExtraInfo = 0x434C_4B50;

    private const int StackInputs = 32;

    /// <inheritdoc />
    [SuppressMessage(
        "ApiDesign",
        "RS0030:Do not use banned APIs",
        Justification = "The product's single SendInput, reached only through InjectionGate (banned-api-exceptions.json: injection)."
    )]
    public unsafe SendResult Send(ReadOnlySpan<LowLevelInput> inputs)
    {
        if (inputs.IsEmpty)
        {
            return new SendResult(0, 0);
        }

        var count = 0;
        foreach (var input in inputs)
        {
            count += input.Kind == LowLevelInputKind.Unicode ? 2 : 1;
        }

        INPUT[]? rented = null;
        var buffer =
            count <= StackInputs
                ? stackalloc INPUT[count]
                : (rented = ArrayPool<INPUT>.Shared.Rent(count)).AsSpan(0, count);
        try
        {
            var desktop = VirtualDesktop.Current();
            var at = 0;
            foreach (var input in inputs)
            {
                at = Fill(buffer, at, input, desktop);
            }

            var inserted = (int)PInvoke.SendInput(buffer, sizeof(INPUT));
            var error = inserted < count ? Marshal.GetLastSystemError() : 0;
            return new SendResult(WholeItems(inputs, inserted), error);
        }
        finally
        {
            if (rented is not null)
            {
                // The buffer may hold typed text: wipe it before it goes back to the pool.
                Array.Clear(rented);
                ArrayPool<INPUT>.Shared.Return(rented);
            }
            else
            {
                buffer.Clear();
            }
        }
    }

    private static int Fill(Span<INPUT> buffer, int at, LowLevelInput input, VirtualDesktop desktop)
    {
        switch (input.Kind)
        {
            case LowLevelInputKind.KeyDown:
            case LowLevelInputKind.KeyUp:
                buffer[at] = Key(input.Key, input.Kind == LowLevelInputKind.KeyUp);
                return at + 1;
            case LowLevelInputKind.Unicode:
                buffer[at] = Unicode(input.Character, up: false);
                buffer[at + 1] = Unicode(input.Character, up: true);
                return at + 2;
            case LowLevelInputKind.MouseMove:
                buffer[at] = Mouse(
                    MOUSE_EVENT_FLAGS.MOUSEEVENTF_MOVE
                        | MOUSE_EVENT_FLAGS.MOUSEEVENTF_ABSOLUTE
                        | MOUSE_EVENT_FLAGS.MOUSEEVENTF_VIRTUALDESK,
                    desktop.NormalizeX(input.X),
                    desktop.NormalizeY(input.Y),
                    0
                );
                return at + 1;
            case LowLevelInputKind.MouseButtonDown:
            case LowLevelInputKind.MouseButtonUp:
                var (flags, data) = Button(
                    input.Button,
                    input.Kind == LowLevelInputKind.MouseButtonUp
                );
                buffer[at] = Mouse(flags, 0, 0, data);
                return at + 1;
            case LowLevelInputKind.Wheel:
                buffer[at] = Mouse(
                    MOUSE_EVENT_FLAGS.MOUSEEVENTF_WHEEL,
                    0,
                    0,
                    unchecked((uint)input.WheelDelta)
                );
                return at + 1;
            case LowLevelInputKind.HorizontalWheel:
                buffer[at] = Mouse(
                    MOUSE_EVENT_FLAGS.MOUSEEVENTF_HWHEEL,
                    0,
                    0,
                    unchecked((uint)input.WheelDelta)
                );
                return at + 1;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(input),
                    input.Kind,
                    "Unknown input kind."
                );
        }
    }

    private static INPUT Key(PhysicalKey key, bool up)
    {
        var scanMode =
            (key.Attributes & LedgerKeyAttributes.ScanCodeMode) != LedgerKeyAttributes.None;
        var flags = (KEYBD_EVENT_FLAGS)0;
        if (scanMode)
        {
            flags |= KEYBD_EVENT_FLAGS.KEYEVENTF_SCANCODE;
        }

        if ((key.Attributes & LedgerKeyAttributes.Extended) != LedgerKeyAttributes.None)
        {
            flags |= KEYBD_EVENT_FLAGS.KEYEVENTF_EXTENDEDKEY;
        }

        if (up)
        {
            flags |= KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP;
        }

        var input = new INPUT { type = INPUT_TYPE.INPUT_KEYBOARD };
        input.Anonymous.ki = new KEYBDINPUT
        {
            wVk = scanMode ? default : (VIRTUAL_KEY)key.Vk,
            wScan = key.Scan,
            dwFlags = flags,
            dwExtraInfo = ExtraInfo,
        };
        return input;
    }

    private static INPUT Unicode(char character, bool up)
    {
        var input = new INPUT { type = INPUT_TYPE.INPUT_KEYBOARD };
        input.Anonymous.ki = new KEYBDINPUT
        {
            wVk = default,
            wScan = character,
            dwFlags = up
                ? KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE | KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP
                : KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE,
            dwExtraInfo = ExtraInfo,
        };
        return input;
    }

    private static INPUT Mouse(MOUSE_EVENT_FLAGS flags, int dx, int dy, uint data)
    {
        var input = new INPUT { type = INPUT_TYPE.INPUT_MOUSE };
        input.Anonymous.mi = new MOUSEINPUT
        {
            dx = dx,
            dy = dy,
            mouseData = data,
            dwFlags = flags,
            dwExtraInfo = ExtraInfo,
        };
        return input;
    }

    private static (MOUSE_EVENT_FLAGS Flags, uint Data) Button(
        LedgerMouseButtons button,
        bool up
    ) =>
        button switch
        {
            LedgerMouseButtons.Left => (
                up ? MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTUP : MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTDOWN,
                0
            ),
            LedgerMouseButtons.Right => (
                up
                    ? MOUSE_EVENT_FLAGS.MOUSEEVENTF_RIGHTUP
                    : MOUSE_EVENT_FLAGS.MOUSEEVENTF_RIGHTDOWN,
                0
            ),
            LedgerMouseButtons.Middle => (
                up
                    ? MOUSE_EVENT_FLAGS.MOUSEEVENTF_MIDDLEUP
                    : MOUSE_EVENT_FLAGS.MOUSEEVENTF_MIDDLEDOWN,
                0
            ),
            LedgerMouseButtons.X1 => (
                up ? MOUSE_EVENT_FLAGS.MOUSEEVENTF_XUP : MOUSE_EVENT_FLAGS.MOUSEEVENTF_XDOWN,
                PInvoke.XBUTTON1
            ),
            LedgerMouseButtons.X2 => (
                up ? MOUSE_EVENT_FLAGS.MOUSEEVENTF_XUP : MOUSE_EVENT_FLAGS.MOUSEEVENTF_XDOWN,
                PInvoke.XBUTTON2
            ),
            _ => throw new ArgumentOutOfRangeException(
                nameof(button),
                button,
                "One button at a time."
            ),
        };

    /// <summary>How many whole items went when <c>SendInput</c> inserted <paramref name="inserted"/> events.</summary>
    private static int WholeItems(ReadOnlySpan<LowLevelInput> inputs, int inserted)
    {
        var events = 0;
        for (var i = 0; i < inputs.Length; i++)
        {
            events += inputs[i].Kind == LowLevelInputKind.Unicode ? 2 : 1;
            if (events > inserted)
            {
                return i;
            }
        }

        return inputs.Length;
    }

    /// <summary>The virtual desktop, to normalize absolute moves.</summary>
    [StructLayout(LayoutKind.Auto)]
    private readonly record struct VirtualDesktop(int Left, int Top, int Width, int Height)
    {
        public static VirtualDesktop Current() =>
            new(
                PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_XVIRTUALSCREEN),
                PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_YVIRTUALSCREEN),
                PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXVIRTUALSCREEN),
                PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYVIRTUALSCREEN)
            );

        public int NormalizeX(int x) => Normalize(x - Left, Width);

        public int NormalizeY(int y) => Normalize(y - Top, Height);

        private static int Normalize(int offset, int size) =>
            (int)
                Math.Round(
                    Math.Clamp(offset, 0, Math.Max(1, size - 1)) * 65535.0 / Math.Max(1, size - 1)
                );
    }
}
