using System.Collections.Immutable;
using Clicalo.App.Interop;
using Clicalo.Application.Coordinators;
using Clicalo.Application.Ports;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Primitives;

namespace Clicalo.App.Composition;

/// <summary>
/// Adds the process and the keyboard layout to an external foreground before the engine sees it (blueprint §7.7,
/// §7.9). Runs on the SysEvents thread and never blocks: both queries are local and answer «unknown» on failure.
/// </summary>
/// <param name="layouts">
/// Builds the layout snapshot of a thread: the engine package's builder (<c>VkKeyScanEx</c>, <c>MapVirtualKeyEx</c>)
/// once integrated, or <see cref="LayoutOnly"/>.
/// </param>
internal sealed class ForegroundDescriber(Func<uint, KeyboardLayoutSnapshot> layouts)
{
    /// <summary>
    /// The layout handle of <paramref name="threadId"/> with no character table: enough for the fixed keys of the
    /// catalog (letters and digits included, <c>keys.win32.json</c>); a character key missing from the table sends
    /// nothing and warns (EC-EJE-10).
    /// </summary>
    /// <param name="threadId">The foreground thread.</param>
    public static KeyboardLayoutSnapshot LayoutOnly(uint threadId) =>
        new(
            new KeyboardLayoutId(unchecked((ulong)NativeMethods.GetKeyboardLayout(threadId))),
            ImmutableDictionary<KeyId, LayoutKey>.Empty
        );

    /// <summary>Describes <paramref name="foreground"/>.</summary>
    /// <param name="foreground">The verified external foreground.</param>
    public ForegroundDetails Describe(ExternalForeground foreground)
    {
        ArgumentNullException.ThrowIfNull(foreground);
        return new ForegroundDetails(
            new ProcessName(ProcessImages.FileNameOf(foreground.AppProcessId) ?? string.Empty),
            layouts(foreground.ThreadId)
        );
    }
}
