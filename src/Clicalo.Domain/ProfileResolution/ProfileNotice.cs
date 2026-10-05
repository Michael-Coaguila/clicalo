using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.ProfileResolution;

/// <summary>The notice an Auto/Fixed change shows (PER-006); the panel localizes it.</summary>
public abstract record ProfileNotice
{
    private ProfileNotice() { }

    /// <summary>«[profLocked]: {profile}».</summary>
    /// <param name="Profile">The profile that stays.</param>
    public sealed record Locked(ProfileId Profile) : ProfileNotice;

    /// <summary>«[profAuto]: {profile}».</summary>
    /// <param name="Profile">The profile of the active app, or General.</param>
    public sealed record FollowingApp(ProfileId Profile) : ProfileNotice;
}
