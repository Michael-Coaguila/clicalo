namespace Clicalo.Application.Interaction;

/// <summary>Data of <see cref="InteractionStore.Changed"/>.</summary>
/// <param name="previous">The state before the change.</param>
/// <param name="current">The state after the change.</param>
public sealed class InteractionChangedEventArgs(InteractionState previous, InteractionState current)
    : EventArgs
{
    /// <summary>The state before the change.</summary>
    public InteractionState Previous { get; } =
        previous ?? throw new ArgumentNullException(nameof(previous));

    /// <summary>The state after the change.</summary>
    public InteractionState Current { get; } =
        current ?? throw new ArgumentNullException(nameof(current));
}
