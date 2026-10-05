using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.ProfileResolution;

/// <summary>What the panel shows (PER-001): Frequents or a profile, never Always visible.</summary>
public abstract record ViewTarget
{
    private ViewTarget() { }

    /// <summary>The Frequents view; it never changes by itself (FRE-003).</summary>
    public sealed record Frequents : ViewTarget;

    /// <summary>A profile.</summary>
    /// <param name="Id">The profile.</param>
    public sealed record Profile(ProfileId Id) : ViewTarget;
}
