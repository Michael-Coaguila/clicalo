namespace Clicalo.Application.Session;

/// <summary>Data of <see cref="SessionStore.Changed"/>.</summary>
/// <param name="previous">The session before the change.</param>
/// <param name="current">The session after the change.</param>
public sealed class SessionChangedEventArgs(PanelSession previous, PanelSession current) : EventArgs
{
    /// <summary>The session before the change.</summary>
    public PanelSession Previous { get; } =
        previous ?? throw new ArgumentNullException(nameof(previous));

    /// <summary>The session after the change.</summary>
    public PanelSession Current { get; } =
        current ?? throw new ArgumentNullException(nameof(current));
}
