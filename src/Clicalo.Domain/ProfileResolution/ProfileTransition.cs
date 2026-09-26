namespace Clicalo.Domain.ProfileResolution;

/// <summary>The result of a profile rule.</summary>
/// <param name="State">The new state.</param>
/// <param name="ResetPage">Whether the panel and the bar go back to page 1 (the view changed, PER-003 step 4).</param>
/// <param name="Notice">The notice to show, if any.</param>
public sealed record ProfileTransition(ProfileState State, bool ResetPage, ProfileNotice? Notice);
