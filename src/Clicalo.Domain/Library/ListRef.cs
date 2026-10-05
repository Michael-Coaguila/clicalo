using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Library;

/// <summary>One of the lists a shortcut can live in (invariant I2).</summary>
public abstract record ListRef
{
    private ListRef() { }

    /// <summary>The Always visible row (docs/02 <c>global</c>).</summary>
    public sealed record AlwaysVisible : ListRef;

    /// <summary>The list of a profile.</summary>
    /// <param name="Id">The profile.</param>
    public sealed record InProfile(ProfileId Id) : ListRef;
}
