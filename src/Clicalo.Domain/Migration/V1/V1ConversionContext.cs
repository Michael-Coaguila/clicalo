using Clicalo.Domain.Catalog;
using Clicalo.Domain.Document;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Migration.V1;

/// <summary>What the pure v1 converter needs from the world (blueprint §6.6).</summary>
/// <param name="Ids">Source of new opaque ids.</param>
/// <param name="Baseline">A new installation's document: settings v1 does not have keep its values (defaults).</param>
/// <param name="Monitors">The monitors, to place <c>window_pos</c>.</param>
/// <param name="Now">The current time.</param>
public sealed record V1ConversionContext(
    IIdGenerator Ids,
    UserDocument Baseline,
    ValueList<V1Monitor> Monitors,
    DateTimeOffset Now
)
{
    /// <summary>
    /// The first icon of <c>suggestIcons(name, keys)</c> (EDI-005) for an imported profile (keys <see langword="null"/>)
    /// or shortcut, or <see langword="null"/> when there is none. Without it the imported items get the fallback
    /// icons of EDI-005 (<c>apps</c> and <c>bolt</c>); either way <c>autoIcon</c> stays on, so the icon keeps
    /// following the name (catalog §7.4).
    /// </summary>
    public Func<string, KeyChord?, IconRef?>? SuggestIcon { get; init; }
}
