using Clicalo.Application.Ports;

namespace Clicalo.Application.Tests.Foreground;

/// <summary>
/// <see cref="IForegroundMonitor"/> driven by the test: <see cref="SwitchTo"/> is a verified external foreground
/// change, as the SysEvents thread would report it after <c>EVENT_SYSTEM_FOREGROUND</c>.
/// </summary>
internal sealed class FakeForegroundMonitor(ForegroundWorld world) : IForegroundMonitor
{
    public event EventHandler<ExternalForegroundChangedEventArgs>? ExternalForegroundChanged;

    public ExternalForeground? Current { get; private set; }

    /// <summary>The user (or a restore) brought <paramref name="window"/> of another process to the front.</summary>
    public void SwitchTo(WindowToken window, bool alsoForeground = true)
    {
        if (alsoForeground)
        {
            world.Control.Foreground = window;
        }

        world.Write("external " + ForegroundWorld.Name(window));
        Current = new ExternalForeground(
            window,
            ProcessId: 4000 + (uint)window.Handle,
            ThreadId: 8000 + (uint)window.Handle,
            world.Time.GetUtcNow()
        );
        ExternalForegroundChanged?.Invoke(this, new ExternalForegroundChangedEventArgs(Current));
    }

    /// <summary>Sets the first external foreground without raising the event (before the orchestrator exists).</summary>
    public void Seed(WindowToken window)
    {
        world.Control.Foreground = window;
        Current = new ExternalForeground(window, 4000, 8000, world.Time.GetUtcNow());
    }

    /// <summary>Number of handlers attached (the orchestrator detaches on dispose).</summary>
    public int Subscribers => ExternalForegroundChanged?.GetInvocationList().Length ?? 0;
}
