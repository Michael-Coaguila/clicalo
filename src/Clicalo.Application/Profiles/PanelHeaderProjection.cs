using Clicalo.Domain.Catalog;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.ProfileResolution;

namespace Clicalo.Application.Profiles;

/// <summary>
/// The pure projection of the panel header (CAB-001 to CAB-003, docs/04 §1): the dynamic title, the «auto» dot and the
/// Auto/Fixed button, from the profile state, the library, the profile of the active app and the search.
/// </summary>
public static class PanelHeaderProjection
{
    /// <summary>The icon of the Frequents title.</summary>
    public static IconRef FrequentsIcon { get; } = new("star");

    /// <summary>The icon of the search title.</summary>
    public static IconRef SearchIcon { get; } = new("search");

    /// <summary>
    /// The header for <paramref name="state"/> (CAB-002): «Buscar» while the search has text; «Frecuentes» in Frequents;
    /// otherwise the icon and the name of the profile shown, with the «auto» dot when it is the profile of the active
    /// app. The Auto/Fixed button hides while the search has text (CAB-001).
    /// </summary>
    /// <param name="state">The profile state.</param>
    /// <param name="library">The shortcuts.</param>
    /// <param name="activeAppProfile">The profile of the active app, or <see langword="null"/> when it has none.</param>
    /// <param name="searchingWithText">Whether the search is open with text.</param>
    /// <param name="language">The interface language.</param>
    /// <param name="fallback">The language used when a name lacks <paramref name="language"/>.</param>
    public static PanelHeaderModel Project(
        ProfileState state,
        ShortcutLibrary library,
        ProfileId? activeAppProfile,
        bool searchingWithText,
        LangCode language,
        LangCode fallback
    )
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(library);
        if (searchingWithText)
        {
            return new PanelHeaderModel(
                HeaderTitleKind.Search,
                L.SearchA,
                string.Empty,
                SearchIcon,
                ShowsActiveAppDot: false,
                state.LockProfile,
                ShowsAutoFixed: false
            );
        }

        if (state.View is not ViewTarget.Profile shown)
        {
            return new PanelHeaderModel(
                HeaderTitleKind.Frequents,
                L.Freq,
                string.Empty,
                FrequentsIcon,
                ShowsActiveAppDot: false,
                state.LockProfile,
                ShowsAutoFixed: true
            );
        }

        var profile = library.TryGetProfile(shown.Id, out var found) ? found : library.General;
        return new PanelHeaderModel(
            HeaderTitleKind.Profile,
            null,
            profile.Name.Get(language, fallback),
            profile.Icon,
            ShowsActiveAppDot: activeAppProfile == profile.Id,
            state.LockProfile,
            ShowsAutoFixed: true
        );
    }

    /// <summary>
    /// The text of a profile notice (PER-006): «Fijo en: {profile}»; «Auto: sigue la app activa: {profile}» when the
    /// active app has a profile, or «Auto: sigue la app activa» when it goes to General.
    /// </summary>
    /// <param name="notice">The notice.</param>
    /// <param name="library">The shortcuts.</param>
    /// <param name="language">The interface language.</param>
    /// <param name="fallback">The language used when a name lacks <paramref name="language"/>.</param>
    public static Message NoticeMessage(
        ProfileNotice notice,
        ShortcutLibrary library,
        LangCode language,
        LangCode fallback
    )
    {
        ArgumentNullException.ThrowIfNull(notice);
        ArgumentNullException.ThrowIfNull(library);
        return notice switch
        {
            ProfileNotice.Locked locked => L.ProfLockedOn(
                profile: NameOf(locked.Profile, library, language, fallback)
            ),
            ProfileNotice.FollowingApp following when following.Profile != ProfileId.General =>
                L.ProfAutoOn(profile: NameOf(following.Profile, library, language, fallback)),
            _ => L.ProfAuto,
        };
    }

    private static string NameOf(
        ProfileId id,
        ShortcutLibrary library,
        LangCode language,
        LangCode fallback
    ) =>
        (library.TryGetProfile(id, out var profile) ? profile : library.General).Name.Get(
            language,
            fallback
        );
}
