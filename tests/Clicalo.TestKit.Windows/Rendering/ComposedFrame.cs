using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using Windows.Win32;

namespace Clicalo.TestKit.Windows.Rendering;

/// <summary>
/// The condition a desktop test waits for between changing what a visible window shows (content that appears, a size
/// that grows) and touching it: WPF ran the layout and the render of the change (everything queued above
/// <see cref="DispatcherPriority.Background"/>, as <see cref="WpfThread.DrainPendingWork"/>) and the desktop window
/// manager then composed a frame (<c>DwmFlush</c>). It is <see cref="FirstFrame"/> for a window that is already on
/// screen: Windows routes a touch to what DWM has composed, not to the window rectangle that <c>WindowFromPoint</c>
/// reads, so a touch on the part of a window that has just grown can fall through to the window below. A person cannot
/// aim at a button before it is on screen, so this is a condition of the test, not of the product.
/// </summary>
public static class ComposedFrame
{
    /// <summary>
    /// Completes when the change already made to <paramref name="window"/> has been rendered and composed; fails when
    /// DWM refuses the flush. Call it from the test thread, never from the window's own.
    /// </summary>
    /// <exception cref="InvalidOperationException">Called on the window's thread, which the flush would block.</exception>
    public static async Task WaitAsync(Window window, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (window.Dispatcher.CheckAccess())
        {
            throw new InvalidOperationException(
                "Wait for the composed frame from the test thread: DwmFlush blocks until the next composition pass."
            );
        }

        await window
            .Dispatcher.InvokeAsync(
                static () => { },
                DispatcherPriority.Background,
                cancellationToken
            )
            .Task.ConfigureAwait(false);
        await Task.Run(
                static () =>
                {
                    var result = PInvoke.DwmFlush();
                    if (result.Failed)
                    {
                        throw Marshal.GetExceptionForHR(result.Value)!;
                    }
                },
                cancellationToken
            )
            .ConfigureAwait(false);
    }
}
