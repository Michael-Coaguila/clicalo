using Clicalo.Application.Ports;

namespace Clicalo.Application.Tests.Coordinators;

/// <summary>A foreground monitor the test drives by hand.</summary>
internal sealed class ScriptedForegroundMonitor : IForegroundMonitor
{
    public ExternalForeground? Current { get; private set; }

    public event EventHandler<ExternalForegroundChangedEventArgs>? ExternalForegroundChanged;

    public int Subscribers => ExternalForegroundChanged?.GetInvocationList().Length ?? 0;

    public void Seed(ExternalForeground foreground) => Current = foreground;

    public void SwitchTo(ExternalForeground foreground)
    {
        Current = foreground;
        ExternalForegroundChanged?.Invoke(this, new ExternalForegroundChangedEventArgs(foreground));
    }
}
