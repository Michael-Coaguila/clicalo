using Clicalo.Domain.Touch;
using Clicalo.Tools.SpikeLab.Scripting;

namespace Clicalo.Tools.SpikeLab.Surfaces;

/// <summary>
/// Receives everything the laboratory surfaces see, on the UI thread: the session executes the tile actions, counts
/// the repetitions and updates the measurements.
/// </summary>
internal interface ILabInputSink
{
    /// <summary>A tile was tapped or commanded by UI Automation.</summary>
    void OnTile(TileInput input);

    /// <summary>A drag of the panel handle ended.</summary>
    void OnHandleDrag(string surface, SurfaceGroup group, PointerKind? pointer);

    /// <summary>A contact did not count (TAC-003); <paramref name="reason"/> says why.</summary>
    void OnIgnoredTouch(string surface, IgnoreReason reason);

    /// <summary>A surface received an activation message (inside its window procedure: must not block).</summary>
    void OnActivationMessage(string surface, string message);
}
