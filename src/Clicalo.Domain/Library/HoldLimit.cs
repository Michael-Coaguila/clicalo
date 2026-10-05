namespace Clicalo.Domain.Library;

/// <summary>
/// The automatic release limit of a held item (SEG-004): the global one, its own, or never (still released by
/// «Release all» and by system events, GEN-012).
/// </summary>
public abstract record HoldLimit
{
    private HoldLimit() { }

    /// <summary>Uses the global limit of the key safety settings (docs/02 <c>maxHold: null</c>).</summary>
    public sealed record InheritGlobal : HoldLimit;

    /// <summary>Its own limit, counted per item from the press.</summary>
    /// <param name="Limit">The limit.</param>
    public sealed record After(TimeSpan Limit) : HoldLimit;

    /// <summary>No time limit.</summary>
    public sealed record Never : HoldLimit;
}
