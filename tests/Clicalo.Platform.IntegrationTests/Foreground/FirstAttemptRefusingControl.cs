using Clicalo.Application.Ports;

namespace Clicalo.Platform.IntegrationTests.Foreground;

/// <summary>
/// The real <see cref="IForegroundControl"/>, except that the next <see cref="TrySetForeground"/> after
/// <see cref="RefuseNextAttempt"/> is refused without calling Windows. It stands in for a missing foreground right:
/// the test process always holds that right, because Windows gives it to the process that injected the last input
/// (spike S4 finding), so step 1 of the ladder cannot fail for real here and step 2 would never run.
/// </summary>
internal sealed class FirstAttemptRefusingControl(IForegroundControl inner) : IForegroundControl
{
    private int _refuseNext;

    /// <summary>Makes the next attempt fail as Windows does without the right: nothing changes.</summary>
    public void RefuseNextAttempt() => Volatile.Write(ref _refuseNext, 1);

    public bool TrySetForeground(WindowToken window) =>
        Interlocked.Exchange(ref _refuseNext, 0) == 0 && inner.TrySetForeground(window);

    public WindowToken GetForeground() => inner.GetForeground();

    public void FlashTaskbar(WindowToken window) => inner.FlashTaskbar(window);
}
