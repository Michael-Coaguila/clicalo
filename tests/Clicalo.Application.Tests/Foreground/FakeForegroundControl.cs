using Clicalo.Application.Ports;

namespace Clicalo.Application.Tests.Foreground;

/// <summary>
/// <see cref="IForegroundControl"/> with a configurable rights policy (blueprint §10.1): every
/// <see cref="TrySetForeground"/> succeeds while <see cref="HasRights"/> holds, unless <see cref="Script"/> dictates
/// the next answers. Each call is written to the shared <see cref="ForegroundWorld.Log"/>.
/// </summary>
internal sealed class FakeForegroundControl(ForegroundWorld world) : IForegroundControl
{
    /// <summary>What <c>GetForegroundWindow</c> returns.</summary>
    public WindowToken Foreground { get; set; }

    /// <summary>
    /// Whether Clícalo received the last input (or WM_HOTKEY). As on Windows, it may also change the foreground while
    /// one of its own windows is in front.
    /// </summary>
    public bool HasRights { get; set; }

    /// <summary>Answers for the next calls, in order; <see cref="HasRights"/> decides once it is empty.</summary>
    public Queue<bool> Script { get; } = new();

    /// <summary>Runs inside each attempt, before it is decided (to observe the orchestrator mid-attempt).</summary>
    public Action<WindowToken>? DuringAttempt { get; set; }

    /// <summary>Every window passed to <see cref="TrySetForeground"/>, in order.</summary>
    public List<WindowToken> Attempts { get; } = [];

    /// <summary>Every window passed to <see cref="FlashTaskbar"/>, in order.</summary>
    public List<WindowToken> Flashed { get; } = [];

    public bool TrySetForeground(WindowToken window)
    {
        Attempts.Add(window);
        DuringAttempt?.Invoke(window);
        var granted =
            Script.Count > 0 ? Script.Dequeue() : HasRights || ForegroundWorld.IsOwn(Foreground);
        world.Write((granted ? "set " : "refused ") + ForegroundWorld.Name(window));
        if (granted)
        {
            Foreground = window;
        }

        return granted;
    }

    public WindowToken GetForeground()
    {
        world.Observe("get foreground");
        return Foreground;
    }

    public void FlashTaskbar(WindowToken window)
    {
        Flashed.Add(window);
        world.Write("flash " + ForegroundWorld.Name(window));
    }
}
