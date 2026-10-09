namespace Clicalo.UI.Wpf.Surfaces;

/// <summary>A surface that says whether a finger, the pen or the pointer is on it (dimming, GEN-009).</summary>
public interface IPointerPresence
{
    /// <summary>Raised when <see cref="IsPointerInside"/> changes.</summary>
    event EventHandler? PresenceChanged;

    /// <summary>Whether a finger, the pen or the pointer is on the surface.</summary>
    bool IsPointerInside { get; }
}
