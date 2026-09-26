using Clicalo.Application.Ports;
using Clicalo.TestKit.Windows;
using Clicalo.TestKit.Windows.Input;

namespace Clicalo.Windowing.IntegrationTests.Foreground.Support;

/// <summary>
/// The M1 test implementation of <see cref="IInternalKeyEffects"/> for the surfaces (spike S4; the engine implements it
/// in M2), with the laboratory safety rule. Win+H goes only to a field of this process that has the keyboard focus:
/// immediately before the batch, the window in front (<c>GetForegroundWindow</c>) must be an own surface whose text field
/// is focused, and <see cref="TestKeyboardInjector"/> checks again that this window owns the foreground, that the batch
/// is balanced and that no modifier is held. The rights chord is never sent: these tests never climb to step 2.
/// </summary>
/// <remarks>
/// The last step of the Win+H batch is <see cref="Sink"/>, not <c>SendInput</c>: a real Win+H opens Windows voice
/// typing with the microphone, which would keep writing whatever it hears into the next window with the focus of the
/// maintainer's machine (and of a runner). Everything before it (the target, the focus, the injector's checks) is the
/// real guard; the real chord is sent in the manual rows of S4.
/// </remarks>
public sealed class GuardedDictationKeys(Func<nint, bool> isOwnFocusedField) : IInternalKeyEffects
{
    private readonly Lock _gate = new();
    private readonly List<(nint Target, IReadOnlyList<KeyStroke> Batch)> _sent = [];
    private int _refused;
    private int _rightsRequests;

    /// <summary>Win+H with left Windows, pressed and released in one batch.</summary>
    public static IReadOnlyList<KeyStroke> DictationChord { get; } =
        KeyStrokes.Chord(VirtualKeyCode.LeftWindows, VirtualKeyCode.H);

    /// <summary>The Win+H batches that passed every check, with the window they were for.</summary>
    public IReadOnlyList<(nint Target, IReadOnlyList<KeyStroke> Batch)> Sent
    {
        get
        {
            lock (_gate)
            {
                return [.. _sent];
            }
        }
    }

    /// <summary>Requests refused by the checks: nothing was sent.</summary>
    public int Refused => Volatile.Read(ref _refused);

    /// <summary>Requests for the rights chord (always refused here).</summary>
    public int RightsRequests => Volatile.Read(ref _rightsRequests);

    public ValueTask<bool> SendRightsHotkeyAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _rightsRequests);
        return ValueTask.FromResult(false);
    }

    public ValueTask<bool> SendDictationChordAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var foreground = ForegroundWindows.Current;
        if (!isOwnFocusedField(foreground))
        {
            return Refuse();
        }

        try
        {
            new TestKeyboardInjector(foreground, batch => Sink(foreground, batch)).Send(
                DictationChord
            );
        }
        catch (InjectionRefusedException)
        {
            return Refuse();
        }

        return ValueTask.FromResult(true);
    }

    /// <summary>Where the checked batch goes instead of <c>SendInput</c> (see the remarks).</summary>
    private void Sink(nint target, IReadOnlyList<KeyStroke> batch)
    {
        lock (_gate)
        {
            _sent.Add((target, batch));
        }
    }

    private ValueTask<bool> Refuse()
    {
        Interlocked.Increment(ref _refused);
        return ValueTask.FromResult(false);
    }
}
