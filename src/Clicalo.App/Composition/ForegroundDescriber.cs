using Clicalo.Application.Coordinators;
using Clicalo.Application.Ports;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Primitives;
using Clicalo.Platform.Windows.SingleInstance;

namespace Clicalo.App.Composition;

/// <summary>
/// Adds the process and the keyboard layout to an external foreground before the engine sees it (blueprint §7.7,
/// §7.9). Runs on the SysEvents thread and never blocks: both queries are local and answer «unknown» on failure.
/// </summary>
/// <param name="layouts">
/// Builds the layout snapshot of a thread: the engine package's builder (<c>VkKeyScanEx</c>, <c>MapVirtualKeyEx</c>,
/// <see cref="EngineAdapters.Layouts"/>).
/// </param>
internal sealed class ForegroundDescriber(Func<uint, KeyboardLayoutSnapshot> layouts)
{
    /// <summary>Describes <paramref name="foreground"/>.</summary>
    /// <param name="foreground">The verified external foreground.</param>
    public ForegroundDetails Describe(ExternalForeground foreground)
    {
        ArgumentNullException.ThrowIfNull(foreground);
        return new ForegroundDetails(
            new ProcessName(ProcessIdentity.ImageFileName(foreground.AppProcessId) ?? string.Empty),
            layouts(foreground.ThreadId)
        );
    }
}
