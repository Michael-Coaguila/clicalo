using Clicalo.Domain.Touch;

namespace Clicalo.UI.Wpf.Pointer;

/// <summary>
/// Receives the frames of one <see cref="PointerInputSource"/> synchronously, on the UI thread, inside the window
/// procedure. Normally a surface that feeds its <see cref="GestureRecognizer"/>; in the tests and SpikeLab, a
/// recorder. Must not block.
/// </summary>
public interface IPointerFrameSink
{
    /// <summary>A pointer frame arrived for the attached window.</summary>
    void OnFrame(in PointerFrame frame);

    /// <summary>
    /// The pointer entered (<paramref name="inside"/> true) or left the window: drives dimming and the pass-through
    /// of the cursor (blueprint §8.3).
    /// </summary>
    void OnHover(bool inside);
}
