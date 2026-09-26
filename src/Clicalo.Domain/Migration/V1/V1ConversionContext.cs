using Clicalo.Domain.Document;
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
);
