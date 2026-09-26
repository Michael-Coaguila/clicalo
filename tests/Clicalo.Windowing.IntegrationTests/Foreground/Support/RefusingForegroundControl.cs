using Clicalo.Application.Ports;

namespace Clicalo.Windowing.IntegrationTests.Foreground.Support;

/// <summary>
/// The real <see cref="IForegroundControl"/>, except that every <see cref="TrySetForeground"/> is refused without calling
/// Windows, as Windows refuses a process without the foreground right. This test process always holds that right,
/// because Windows gives it to the process that injected the last input (spike S4, finding 2), so a real refusal cannot
/// be produced here.
/// </summary>
public sealed class RefusingForegroundControl(IForegroundControl inner) : IForegroundControl
{
    private int _attempts;
    private int _flashes;

    /// <summary>Attempts refused so far.</summary>
    public int Attempts => Volatile.Read(ref _attempts);

    /// <summary>Taskbar flashes asked for so far.</summary>
    public int Flashes => Volatile.Read(ref _flashes);

    public bool TrySetForeground(WindowToken window)
    {
        Interlocked.Increment(ref _attempts);
        return false;
    }

    public WindowToken GetForeground() => inner.GetForeground();

    public void FlashTaskbar(WindowToken window) => Interlocked.Increment(ref _flashes);
}
