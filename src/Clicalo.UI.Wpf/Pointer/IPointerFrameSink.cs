using Clicalo.Domain.Touch;

namespace Clicalo.UI.Wpf.Pointer;

/// <summary>
/// Receives the frames of one <see cref="PointerInputSource"/> synchronously, on the UI thread, inside the window
/// procedure. Normally a surface that feeds its <see cref="GestureRecognizer"/> (<see cref="GestureHost"/>); in the
/// tests and SpikeLab, a recorder. Must not block and must not pump messages.
/// </summary>
public interface IPointerFrameSink
{
    /// <summary>A pointer frame arrived for the attached window.</summary>
    /// <remarks>
    /// So that the window procedure never allocates, the source reuses the array behind
    /// <see cref="PointerFrame.Samples"/>: the samples are only valid during this call. A sink that keeps a frame
    /// (a recorder, another thread) copies them first, for example
    /// <c>frame with { Samples = [.. frame.Samples] }</c>.
    /// </remarks>
    void OnFrame(in PointerFrame frame);

    /// <summary>
    /// The pointer entered (<paramref name="inside"/> true) or left the window: drives dimming and the pass-through
    /// of the cursor (blueprint §8.3).
    /// </summary>
    void OnHover(bool inside);
}
