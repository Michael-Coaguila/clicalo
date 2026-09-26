using Clicalo.Domain.Catalog;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Migration.V1;

/// <summary>
/// What an imported button will do, decided before any id or chord is built (catalog §7.4): the pure half of the
/// conversion, so every rule can be checked without the model's factories.
/// </summary>
internal abstract record V1ActionPlan
{
    private V1ActionPlan() { }

    /// <summary>Keys: none is an incomplete Tap, one chord a Tap, several chords a Macro with one step per chord.</summary>
    /// <param name="Chords">The strokes of each chord, in order.</param>
    public sealed record KeyPresses(ValueList<ValueList<KeyStroke>> Chords) : V1ActionPlan;

    /// <summary>A web address.</summary>
    /// <param name="Target">The address, valid or kept raw for review.</param>
    public sealed record Url(UrlTarget Target) : V1ActionPlan;

    /// <summary>An app, never through an interpreter.</summary>
    /// <param name="Target">What to start, or the raw command kept for review.</param>
    public sealed record App(AppTarget Target) : V1ActionPlan;

    /// <summary>A system action (Win+L becomes «Lock computer», MIG-007).</summary>
    /// <param name="Command">The command.</param>
    /// <param name="Icon">Its icon in <c>data/catalogs/system-commands.json</c>.</param>
    public sealed record SystemCommand(SystemCommandId Command, IconRef Icon) : V1ActionPlan;
}
