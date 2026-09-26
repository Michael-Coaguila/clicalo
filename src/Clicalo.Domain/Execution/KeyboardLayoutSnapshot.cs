using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Clicalo.Domain.Keys;

namespace Clicalo.Domain.Execution;

/// <summary>
/// A pure table of the foreground thread's layout, captured on every foreground change and on
/// <c>WM_INPUTLANGCHANGE</c> (blueprint §7.7). Fixed keys take <c>vk</c>, <c>scan</c> and <c>extended</c> from
/// <c>keys.win32.json</c>; characters come from this table. A key missing from the layout sends nothing and warns
/// (EC-EJE-10).
/// </summary>
/// <param name="Layout">The layout.</param>
/// <param name="Characters">How each character key of the catalog is typed in this layout.</param>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality instead of throwing NotImplementedException",
    Justification = "M2 contract; the engine package implements it (docs/testing/spikes/M2-ownership.md)."
)]
public sealed record KeyboardLayoutSnapshot(
    KeyboardLayoutId Layout,
    ImmutableDictionary<KeyId, LayoutKey> Characters
)
{
    /// <summary>Resolves a stroke to the physical key for <paramref name="mode"/> (table of blueprint §7.7).</summary>
    /// <param name="stroke">The stroke; a side selects the left or right key.</param>
    /// <param name="mode">The mode of the plan.</param>
    /// <param name="key">The physical key.</param>
    public bool TryResolve(KeyStroke stroke, InjectionMode mode, out InjectedKey key) =>
        throw new NotImplementedException();
}
