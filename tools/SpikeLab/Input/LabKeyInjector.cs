using System.Globalization;
using System.Runtime.InteropServices;
using Clicalo.TestKit.Windows.Input;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Clicalo.Tools.SpikeLab.Input;

/// <summary>
/// The minimal injector of the laboratory panel: it sends the chord of a tile to the app in front with one atomic
/// <c>SendInput</c>, only while «Enviar teclas» is on. It is lab code, never the product injector (Platform.Core,
/// M2). Safety rules, all checked before a single event is sent:
/// <list type="number">
/// <item>the batch is balanced (<see cref="KeyStrokeBatch.Validate"/>): every press is released in the same batch;</item>
/// <item>it only contains left-hand modifiers: never AltGr, right Ctrl or any right-hand modifier;</item>
/// <item>there is a foreground window, and it is the same immediately before the call;</item>
/// <item>no modifier is held (physically or by a dictation tool), so the batch cannot combine with it.</item>
/// </list>
/// </summary>
internal sealed class LabKeyInjector
{
    /// <summary><c>dwExtraInfo</c> of every event this injector sends ("CLLB"), so InputProbe can tell them apart.</summary>
    public const long ExtraInfoMarker = 0x434C_4C42;

    private static readonly (VirtualKeyCode Key, string Name)[] Modifiers =
    [
        (VirtualKeyCode.LeftShift, "Mayús izquierda"),
        (VirtualKeyCode.RightShift, "Mayús derecha"),
        (VirtualKeyCode.LeftControl, "Ctrl izquierda"),
        (VirtualKeyCode.RightControl, "Ctrl derecha"),
        (VirtualKeyCode.LeftMenu, "Alt"),
        (VirtualKeyCode.RightMenu, "AltGr"),
        (VirtualKeyCode.LeftWindows, "Windows izquierda"),
        (VirtualKeyCode.RightWindows, "Windows derecha"),
    ];

    /// <summary>The only keys «Soltar todo» may release: left Shift, Ctrl and Alt (Windows would open Start).</summary>
    private static readonly VirtualKeyCode[] Releasable =
    [
        VirtualKeyCode.LeftShift,
        VirtualKeyCode.LeftControl,
        VirtualKeyCode.LeftMenu,
    ];

    private readonly Func<nint> _foreground;
    private readonly Func<VirtualKeyCode, bool> _isDown;
    private readonly Action<IReadOnlyList<KeyStroke>, nint> _send;

    /// <summary>Creates the injector over the real Win32 calls.</summary>
    public LabKeyInjector()
        : this(ForegroundWindow, IsKeyDown, SendBatch) { }

    /// <summary>Creates the injector over the given calls (tests replace <c>SendInput</c>).</summary>
    /// <param name="foreground"><c>GetForegroundWindow</c>.</param>
    /// <param name="isDown"><c>GetAsyncKeyState</c> for one key.</param>
    /// <param name="send">Sends the resolved batch to the window.</param>
    internal LabKeyInjector(
        Func<nint> foreground,
        Func<VirtualKeyCode, bool> isDown,
        Action<IReadOnlyList<KeyStroke>, nint> send
    )
    {
        _foreground = foreground;
        _isDown = isDown;
        _send = send;
    }

    /// <summary>Sends <paramref name="chord"/> to the app in front, or says why nothing was sent.</summary>
    public InjectionOutcome Send(LabChord chord)
    {
        var batch = chord.ToBatch();
        KeyStrokeBatch.Validate(batch);
        EnsureLeftHandOnly(batch);

        var target = _foreground();
        if (target == 0)
        {
            return InjectionOutcome.Refused(
                "No hay ninguna ventana en primer plano: no se envió nada."
            );
        }

        foreach (var (key, name) in Modifiers)
        {
            if (_isDown(key))
            {
                return InjectionOutcome.Refused(
                    "La tecla "
                        + name
                        + " está pulsada (a mano o por otro programa): no se envió nada para no combinarse con ella."
                );
            }
        }

        // Last check immediately before the call, so a foreground change cannot redirect the batch.
        if (_foreground() != target)
        {
            return InjectionOutcome.Refused(
                "El primer plano cambió justo antes de enviar: no se envió nada."
            );
        }

        try
        {
            _send(batch, target);
        }
        catch (InjectionRefusedException ex)
        {
            return InjectionOutcome.Refused(ex.Message);
        }

        return InjectionOutcome.Done;
    }

    /// <summary>
    /// «Soltar todo»: releases left Shift, Ctrl and Alt when they are down (key-ups only; never a press, never a
    /// right-hand key and never Windows, whose lone release would open Start).
    /// </summary>
    /// <returns>The keys released.</returns>
    public IReadOnlyList<VirtualKeyCode> ReleaseHeldModifiers()
    {
        var held = Releasable.Where(_isDown).ToArray();
        if (held.Length == 0)
        {
            return [];
        }

        try
        {
            _send([.. held.Select(KeyStroke.Release)], _foreground());
        }
        catch (InjectionRefusedException)
        {
            return [];
        }

        return held;
    }

    /// <summary>
    /// Throws when <paramref name="batch"/> contains a right-hand modifier or a generic one whose side the system
    /// would decide (the laboratory never injects AltGr or right Ctrl on the maintainer's machine).
    /// </summary>
    public static void EnsureLeftHandOnly(IReadOnlyList<KeyStroke> batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        foreach (var stroke in batch)
        {
            if (
                stroke.VirtualKey
                is VirtualKeyCode.RightControl
                    or VirtualKeyCode.RightMenu
                    or VirtualKeyCode.RightShift
                    or VirtualKeyCode.RightWindows
                    or VirtualKeyCode.Control
                    or VirtualKeyCode.Menu
                    or VirtualKeyCode.Shift
            )
            {
                throw new ArgumentException(
                    "The laboratory only injects left-hand modifiers: " + stroke,
                    nameof(batch)
                );
            }
        }
    }

    private static nint ForegroundWindow() => PInvoke.GetForegroundWindow();

    private static bool IsKeyDown(VirtualKeyCode key) =>
        (PInvoke.GetAsyncKeyState((int)key) & 0x8000) != 0;

    private static void SendBatch(IReadOnlyList<KeyStroke> batch, nint target)
    {
        var layout = KeyboardLayouts.OfWindow(target);
        var inputs = new INPUT[batch.Count];
        for (var i = 0; i < batch.Count; i++)
        {
            var stroke = batch[i];
            var (scanCode, extended) =
                stroke.IsUnicode || stroke.IsScanCodeMode
                    ? (stroke.ScanCode, stroke.IsExtended)
                    : KeyboardLayouts.ToScanCode(stroke.VirtualKey, layout);
            var flags =
                stroke.Flags | (extended ? KeyStrokeOptions.ExtendedKey : KeyStrokeOptions.None);
            inputs[i] = new INPUT { type = INPUT_TYPE.INPUT_KEYBOARD };
            inputs[i].Anonymous.ki = new KEYBDINPUT
            {
                wVk = (VIRTUAL_KEY)stroke.VirtualKey,
                wScan = scanCode,
                dwFlags = (KEYBD_EVENT_FLAGS)(uint)flags,
                time = 0,
                dwExtraInfo = (nuint)ExtraInfoMarker,
            };
        }

        uint inserted;
        unsafe
        {
            inserted = PInvoke.SendInput(inputs, sizeof(INPUT));
        }

        if (inserted != inputs.Length)
        {
            throw new InjectionRefusedException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"SendInput insertó {inserted} de {inputs.Length} eventos (error Win32 {Marshal.GetLastPInvokeError()}): la app en primer plano puede ser de administrador (UIPI) o el escritorio está bloqueado."
                )
            );
        }
    }
}
