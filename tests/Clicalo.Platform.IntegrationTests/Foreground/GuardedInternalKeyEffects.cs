using Clicalo.Application.Ports;
using Clicalo.Platform.Windows.Foreground;
using Clicalo.TestKit.Windows;
using Clicalo.TestKit.Windows.Input;

namespace Clicalo.Platform.IntegrationTests.Foreground;

/// <summary>
/// The M1 test implementation of <see cref="IInternalKeyEffects"/> (spike S4; the engine implements it in M2). It
/// follows the laboratory safety rule: immediately before each batch it checks with <c>GetForegroundWindow</c> that
/// the foreground is one of the allowed windows (InputProbe or a window of this test process) and otherwise injects
/// nothing; each batch carries its releases (<see cref="TestKeyboardInjector"/> refuses unbalanced batches, a held
/// modifier or a foreground change); and it only ever uses LEFT modifiers, never AltGr or right Ctrl.
/// </summary>
internal sealed class GuardedInternalKeyEffects(
    IInternalRightsHotkey rightsHotkey,
    Func<nint, bool> isAllowedTarget,
    Func<nint, bool> isOwnWindow
) : IInternalKeyEffects
{
    /// <summary><c>VK_F24</c>, the key of the reserved chord.</summary>
    public const VirtualKeyCode F24 = (VirtualKeyCode)InternalRightsHotkey.VirtualKey;

    private int _rightsChords;
    private int _refused;

    /// <summary>Rights chords actually injected.</summary>
    public int RightsChords => Volatile.Read(ref _rightsChords);

    /// <summary>Requests refused by the safety checks (nothing was injected).</summary>
    public int Refused => Volatile.Read(ref _refused);

    /// <summary>Ctrl+Alt+Shift+F24 with left modifiers, pressed in that order and released in reverse.</summary>
    public static IReadOnlyList<KeyStroke> RightsChord { get; } =
        KeyStrokes.Chord(
            VirtualKeyCode.LeftControl,
            VirtualKeyCode.LeftMenu,
            VirtualKeyCode.LeftShift,
            F24
        );

    public ValueTask<bool> SendRightsHotkeyAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!rightsHotkey.IsRegistered)
        {
            // Unregistered, the chord would reach the foreground app.
            return Refuse();
        }

        return Send(RightsChord, isAllowedTarget, ref _rightsChords);
    }

    public ValueTask<bool> SendDictationChordAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var unused = 0;

        // Win+H only for a field of this process: with the probe in front it is refused (spike S4, DictationTests).
        return Send(
            KeyStrokes.Chord(VirtualKeyCode.LeftWindows, VirtualKeyCode.H),
            isOwnWindow,
            ref unused
        );
    }

    private ValueTask<bool> Send(
        IReadOnlyList<KeyStroke> batch,
        Func<nint, bool> allowed,
        ref int sent
    )
    {
        var foreground = ForegroundWindows.Current;
        if (!allowed(foreground))
        {
            return Refuse();
        }

        try
        {
            // The injector checks again, immediately before SendInput, that this window owns the foreground.
            new TestKeyboardInjector(foreground).Send(batch);
        }
        catch (InjectionRefusedException)
        {
            return Refuse();
        }

        Interlocked.Increment(ref sent);
        return ValueTask.FromResult(true);
    }

    private ValueTask<bool> Refuse()
    {
        Interlocked.Increment(ref _refused);
        return ValueTask.FromResult(false);
    }
}
