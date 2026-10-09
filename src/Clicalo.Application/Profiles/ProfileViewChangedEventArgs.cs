using Clicalo.Domain.ProfileResolution;

namespace Clicalo.Application.Profiles;

/// <summary>Data of <see cref="ProfileViewCoordinator.Changed"/>.</summary>
/// <param name="previous">The state before.</param>
/// <param name="current">The state after.</param>
/// <param name="resetPage">The view changed: the panel and the bar go back to page 1 (PER-003 step 4, CUA-006).</param>
/// <param name="notice">The notice of an Auto/Fixed change (PER-006), if any.</param>
/// <param name="appChanged">
/// The active app really changed: the search closes and empties (PER-003 step 5, BUS-001) and the context menu closes
/// (PAN-008), whether or not the view moved.
/// </param>
public sealed class ProfileViewChangedEventArgs(
    ProfileState previous,
    ProfileState current,
    bool resetPage,
    ProfileNotice? notice,
    bool appChanged
) : EventArgs
{
    /// <summary>The state before.</summary>
    public ProfileState Previous { get; } =
        previous ?? throw new ArgumentNullException(nameof(previous));

    /// <summary>The state after.</summary>
    public ProfileState Current { get; } =
        current ?? throw new ArgumentNullException(nameof(current));

    /// <summary>Whether the panel and the bar go back to page 1.</summary>
    public bool ResetPage { get; } = resetPage;

    /// <summary>The notice to show, if any.</summary>
    public ProfileNotice? Notice { get; } = notice;

    /// <summary>Whether the active app really changed.</summary>
    public bool AppChanged { get; } = appChanged;
}
