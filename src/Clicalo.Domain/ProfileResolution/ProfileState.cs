using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.ProfileResolution;

/// <summary>
/// The profile state of the panel (PER-001): the view, which lives in the session, plus Auto/Fixed and the last
/// profile, which are settings (<c>lockProfile</c>, <c>lastProfile</c>).
/// </summary>
/// <param name="View">What the panel shows.</param>
/// <param name="LockProfile">Fixed (<see langword="true"/>, red) or Auto (blue).</param>
/// <param name="LastProfile">The profile chosen last, used to return from Frequents (PER-004).</param>
public sealed record ProfileState(ViewTarget View, bool LockProfile, ProfileId? LastProfile);
