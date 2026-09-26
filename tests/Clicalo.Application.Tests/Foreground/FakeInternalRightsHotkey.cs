using Clicalo.Application.Ports;

namespace Clicalo.Application.Tests.Foreground;

/// <summary>
/// <see cref="IInternalRightsHotkey"/> whose <c>WM_HOTKEY</c> the test (or <see cref="FakeInternalKeyEffects"/>)
/// delivers with <see cref="Arrive"/>, or lets time out with <see cref="TimeOut"/>.
/// </summary>
internal sealed class FakeInternalRightsHotkey(ForegroundWorld world) : IInternalRightsHotkey
{
    private TaskCompletionSource<bool>? _pending;

    public bool IsRegistered { get; set; } = true;

    /// <summary>Number of waits armed so far.</summary>
    public int Armed { get; private set; }

    /// <summary>True while a wait is armed and not completed.</summary>
    public bool IsArmed => _pending is { Task.IsCompleted: false };

    public ValueTask<bool> WaitForRightsAsync(CancellationToken cancellationToken)
    {
        Armed++;
        world.Write("arm");
        var pending = new TaskCompletionSource<bool>();
        _pending = pending;
        cancellationToken.Register(() =>
        {
            if (pending.TrySetCanceled(cancellationToken))
            {
                world.Write("disarm");
            }
        });
        return new ValueTask<bool>(pending.Task);
    }

    /// <summary>What waiting for the release of the chord answers; true (every key up) by default.</summary>
    public Func<bool> ChordReleased { get; set; } = static () => true;

    public ValueTask<bool> WaitForChordReleaseAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        world.Write("wait for release");
        return new ValueTask<bool>(ChordReleased());
    }

    /// <summary>The <c>WM_HOTKEY</c> of the reserved chord arrives.</summary>
    public void Arrive()
    {
        world.Write("hotkey");
        _pending?.TrySetResult(true);
    }

    /// <summary><c>Timings.Foreground.RightsHotkeyTimeout</c> elapses without it.</summary>
    public void TimeOut() => _pending?.TrySetResult(false);
}
