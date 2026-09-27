using Clicalo.Application.Engine;

namespace Clicalo.Application.Coordinators;

/// <summary>Data of <see cref="EngineObserverRelay.SnapshotChanged"/>.</summary>
/// <param name="snapshot">The newest snapshot of the engine.</param>
public sealed class EngineSnapshotEventArgs(EngineSnapshot snapshot) : EventArgs
{
    /// <summary>The newest snapshot of the engine.</summary>
    public EngineSnapshot Snapshot { get; } =
        snapshot ?? throw new ArgumentNullException(nameof(snapshot));
}
