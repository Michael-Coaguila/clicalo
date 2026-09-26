namespace Clicalo.Application.Ports;

/// <summary>Data of <see cref="IForegroundMonitor.ExternalForegroundChanged"/>.</summary>
/// <param name="foreground">The new external foreground window.</param>
public sealed class ExternalForegroundChangedEventArgs(ExternalForeground foreground) : EventArgs
{
    /// <summary>The new external foreground window.</summary>
    public ExternalForeground Foreground { get; } =
        foreground ?? throw new ArgumentNullException(nameof(foreground));
}
