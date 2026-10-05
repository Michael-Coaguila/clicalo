using System.Runtime.InteropServices;
using System.Windows;
using Windows.Win32;

namespace Clicalo.TestKit.Windows.Rendering;

/// <summary>
/// The condition a desktop test waits for between showing a window and touching it: WPF rendered the window's first
/// frame (<see cref="Window.ContentRendered"/>) and the desktop window manager composed a frame after it
/// (<c>DwmFlush</c>). Windows routes a touch to what DWM has composed, not to what <c>WindowFromPoint</c> sees: on the
/// CI runner a surface tapped 0–15 ms after <c>ShowPassive</c> almost never received the touch even with
/// <c>ContentRendered</c> already raised, while one tapped after this condition always did (200 of 200, first-frame
/// measurement of m2/fix-desk, S1.md). A touch sent sooner falls through to the window below, which may belong to
/// another application although <c>SyntheticPointer</c>'s check saw the new window under the point: in
/// <c>NonActivationTests</c> it activated the runner's terminal. A person cannot aim at a window before it is on
/// screen, so this is a condition of the test, not of the product.
/// </summary>
public static class FirstFrame
{
    /// <summary>
    /// Starts watching <paramref name="window"/>, which must not be visible yet: call it on the window's thread just
    /// before its first show. The task completes when the first frame is composed, and fails when DWM refuses the
    /// flush.
    /// </summary>
    /// <exception cref="InvalidOperationException">The window is already visible (its first frame may be gone).</exception>
    public static Task Watch(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        window.VerifyAccess();
        if (window.IsVisible)
        {
            throw new InvalidOperationException(
                "Watch the first frame before the window is shown: ContentRendered is raised only once."
            );
        }

        var composed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler? rendered = null;
        rendered = (_, _) =>
        {
            window.ContentRendered -= rendered;

            // DwmFlush blocks until the next composition pass: never on the window's own thread.
            _ = Task.Run(() =>
            {
                var result = PInvoke.DwmFlush();
                if (result.Failed)
                {
                    composed.TrySetException(Marshal.GetExceptionForHR(result.Value)!);
                }
                else
                {
                    composed.TrySetResult();
                }
            });
        };
        window.ContentRendered += rendered;
        return composed.Task;
    }
}
