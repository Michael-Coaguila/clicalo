using Clicalo.Application.Ports;
using Clicalo.TestKit.Windows.Input;

namespace Clicalo.Tools.SpikeLab.Input;

/// <summary>
/// The laboratory implementation of <see cref="IInternalKeyEffects"/> (S4.md, «Regla de seguridad del laboratorio»):
/// it sends the internal rights chord (Ctrl+Alt+Shift+F24, left modifiers) and Win+H ONLY when the window in front is
/// a window of the laboratory or InputProbe, checked immediately before each batch; every batch is balanced and
/// never contains AltGr or right Ctrl. The engine implements the port in M2.
/// </summary>
internal sealed class LabInternalKeyEffects : IInternalKeyEffects
{
    private readonly Func<IInternalRightsHotkey?> _rightsHotkey;
    private readonly Func<nint> _foreground;
    private readonly Func<nint, bool> _isAllowedTarget;
    private readonly Func<nint, IReadOnlyList<KeyStroke>, InjectionOutcome> _send;
    private readonly Action<string> _onRefused;
    private readonly Action _onRightsChord;

    /// <summary>Creates the effects.</summary>
    /// <param name="rightsHotkey">The registration of the chord; null or failing means not registered.</param>
    /// <param name="foreground"><c>GetForegroundWindow</c>.</param>
    /// <param name="isAllowedTarget">True for a window of the laboratory or InputProbe (<see cref="LabTargetPolicy"/>).</param>
    /// <param name="send">Sends a balanced batch to that window, checking again that it is in front.</param>
    /// <param name="onRefused">Told why a batch was not sent.</param>
    /// <param name="onRightsChord">Told after the rights chord was injected (the ladder used step 2).</param>
    public LabInternalKeyEffects(
        Func<IInternalRightsHotkey?> rightsHotkey,
        Func<nint> foreground,
        Func<nint, bool> isAllowedTarget,
        Func<nint, IReadOnlyList<KeyStroke>, InjectionOutcome> send,
        Action<string> onRefused,
        Action onRightsChord
    )
    {
        _rightsHotkey = rightsHotkey;
        _foreground = foreground;
        _isAllowedTarget = isAllowedTarget;
        _send = send;
        _onRefused = onRefused;
        _onRightsChord = onRightsChord;
    }

    /// <summary>The reserved rights chord of blueprint §3.6.</summary>
    public static LabChord RightsChord { get; } =
        LabChord.Of(LabModifiers.Control | LabModifiers.Alt | LabModifiers.Shift, LabChord.F24Key);

    /// <summary>Win+H: Windows dictation for the focused field.</summary>
    public static LabChord DictationChord { get; } =
        LabChord.Of(LabModifiers.Windows, VirtualKeyCode.H);

    /// <summary>
    /// Sends a batch to <paramref name="target"/> with the guarded test injector of TestKit.Windows, which checks
    /// again that the target owns the foreground and that no modifier is held.
    /// </summary>
    public static InjectionOutcome SendGuarded(nint target, IReadOnlyList<KeyStroke> batch)
    {
        try
        {
            new TestKeyboardInjector(target).Send(batch);
            return InjectionOutcome.Done;
        }
        catch (InjectionRefusedException ex)
        {
            return InjectionOutcome.Refused(ex.Message);
        }
    }

    /// <inheritdoc />
    public ValueTask<bool> SendRightsHotkeyAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsRegistered())
        {
            _onRefused("El atajo interno de derechos no está registrado: no se envió.");
            return ValueTask.FromResult(false);
        }

        var sent = SendToOwnOrProbe(RightsChord, "el atajo interno de derechos");
        if (sent)
        {
            _onRightsChord();
        }

        return ValueTask.FromResult(sent);
    }

    /// <inheritdoc />
    public ValueTask<bool> SendDictationChordAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(SendToOwnOrProbe(DictationChord, "Win+H"));
    }

    private bool IsRegistered() => _rightsHotkey() is { IsRegistered: true };

    private bool SendToOwnOrProbe(LabChord chord, string what)
    {
        var batch = chord.ToBatch();
        LabKeyInjector.EnsureLeftHandOnly(batch);
        var target = _foreground();
        if (target == 0 || !_isAllowedTarget(target))
        {
            _onRefused(
                "No se envió "
                    + what
                    + ": delante no hay una ventana de SpikeLab ni la sonda (regla de seguridad del laboratorio)."
            );
            return false;
        }

        var outcome = _send(target, batch);
        if (!outcome.Sent)
        {
            _onRefused("No se envió " + what + ": " + outcome.Reason);
        }

        return outcome.Sent;
    }
}
