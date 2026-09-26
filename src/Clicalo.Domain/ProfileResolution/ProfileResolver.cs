using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.ProfileResolution;

/// <summary>
/// Which profile the panel shows (PER-001 to PER-008, docs/03 §2): pure rules over the view × Auto/Fixed × active app
/// table. The active app is the last real foreground process: Clícalo's windows, the shell and the touch keyboard are
/// filtered out before these rules see it (PER-003), and <see langword="null"/> means none is known yet.
/// </summary>
public static class ProfileResolver
{
    /// <summary>
    /// The profile of an app (PER-002, PER-003): <c>profileFor(process)</c>, or General when the app has none.
    /// </summary>
    /// <param name="library">The shortcuts.</param>
    /// <param name="app">The active app.</param>
    public static ProfileId ProfileOfApp(ShortcutLibrary library, ProcessName? app)
    {
        ArgumentNullException.ThrowIfNull(library);
        return app is { } process && library.ProfileFor(process) is { } profile
            ? profile.Id
            : ProfileId.General;
    }

    /// <summary>
    /// The return profile (retProf, PER-004), in order: the active app's profile when in Auto and it has one; the last
    /// profile if it still exists; the active app's profile if it has one; General.
    /// </summary>
    /// <param name="state">The profile state.</param>
    /// <param name="library">The shortcuts.</param>
    /// <param name="app">The active app.</param>
    public static ProfileId ReturnProfile(
        ProfileState state,
        ShortcutLibrary library,
        ProcessName? app
    )
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(library);
        var appProfile = app is { } process ? library.ProfileFor(process) : null;
        if (!state.LockProfile && appProfile is not null)
        {
            return appProfile.Id;
        }

        if (state.LastProfile is { } last && library.TryGetProfile(last, out _))
        {
            return last;
        }

        return appProfile?.Id ?? ProfileId.General;
    }

    /// <summary>
    /// The active app really changed (PER-003): in Auto and outside Frequents the view becomes the app's profile or
    /// General; Frequents and Fixed never change by themselves. A manual choice lasts until this happens (PER-005).
    /// </summary>
    /// <param name="state">The profile state.</param>
    /// <param name="library">The shortcuts.</param>
    /// <param name="app">The new active app.</param>
    public static ProfileTransition OnAppChanged(
        ProfileState state,
        ShortcutLibrary library,
        ProcessName app
    )
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.LockProfile || state.View is ViewTarget.Frequents)
        {
            return new ProfileTransition(state, ResetPage: false, Notice: null);
        }

        return Show(state, ProfileOfApp(library, app), notice: null);
    }

    /// <summary>The ★ Frequents button (docs/03 §2).</summary>
    /// <param name="state">The profile state.</param>
    public static ProfileTransition ShowFrequents(ProfileState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var view = new ViewTarget.Frequents();
        return new ProfileTransition(
            state with
            {
                View = view,
            },
            ResetPage: !state.View.Equals(view),
            Notice: null
        );
    }

    /// <summary>The profile button while in Frequents: one tap back to the return profile (PER-004).</summary>
    /// <param name="state">The profile state.</param>
    /// <param name="library">The shortcuts.</param>
    /// <param name="app">The active app.</param>
    public static ProfileTransition ReturnFromFrequents(
        ProfileState state,
        ShortcutLibrary library,
        ProcessName? app
    ) => Show(state, ReturnProfile(state, library, app), notice: null);

    /// <summary>
    /// A profile chosen in the selector: it is shown and becomes the last profile (docs/03 §2). An unknown profile
    /// changes nothing.
    /// </summary>
    /// <param name="state">The profile state.</param>
    /// <param name="library">The shortcuts.</param>
    /// <param name="id">The chosen profile.</param>
    public static ProfileTransition Choose(
        ProfileState state,
        ShortcutLibrary library,
        ProfileId id
    )
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(library);
        if (!library.TryGetProfile(id, out _))
        {
            return new ProfileTransition(state, ResetPage: false, Notice: null);
        }

        return Show(state with { LastProfile = id }, id, notice: null);
    }

    /// <summary>
    /// The Auto/Fixed button, or the «Follow the active app» switch, which is the same setting (PER-006):
    /// <list type="bullet">
    /// <item>Auto → Fixed fixes the shown profile, which also becomes the last profile; in Frequents it fixes the
    /// return profile, saves it as the last profile and stays in Frequents. Notice «[profLocked]: {profile}».</item>
    /// <item>Fixed → Auto jumps to the active app's profile or General, on page 1, unless in Frequents. Notice
    /// «[profAuto]: {profile}».</item>
    /// </list>
    /// </summary>
    /// <param name="state">The profile state.</param>
    /// <param name="library">The shortcuts.</param>
    /// <param name="app">The active app.</param>
    public static ProfileTransition ToggleLock(
        ProfileState state,
        ShortcutLibrary library,
        ProcessName? app
    )
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!state.LockProfile)
        {
            var fixedProfile = state.View is ViewTarget.Profile shown
                ? shown.Id
                : ReturnProfile(state, library, app);
            return new ProfileTransition(
                state with
                {
                    LockProfile = true,
                    LastProfile = fixedProfile,
                },
                ResetPage: false,
                new ProfileNotice.Locked(fixedProfile)
            );
        }

        var appProfile = ProfileOfApp(library, app);
        var unlocked = state with { LockProfile = false };
        var notice = new ProfileNotice.FollowingApp(appProfile);
        if (state.View is ViewTarget.Frequents)
        {
            return new ProfileTransition(unlocked, ResetPage: false, notice);
        }

        return new ProfileTransition(
            unlocked with
            {
                View = new ViewTarget.Profile(appProfile),
            },
            ResetPage: true,
            notice
        );
    }

    /// <summary>
    /// A template of the active app was installed as <paramref name="installed"/>: the view moves to it only in Auto
    /// and outside Frequents (PER-007).
    /// </summary>
    /// <param name="state">The profile state.</param>
    /// <param name="installed">The new profile.</param>
    public static ProfileTransition OnTemplateInstalled(ProfileState state, ProfileId installed)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.LockProfile || state.View is ViewTarget.Frequents
            ? new ProfileTransition(state, ResetPage: false, Notice: null)
            : Show(state, installed, notice: null);
    }

    /// <summary>
    /// A profile was deleted (PER-008): if it was shown, the panel goes to General (still Fixed if it was Fixed); if it
    /// was the last profile, the last profile becomes General.
    /// </summary>
    /// <param name="state">The profile state.</param>
    /// <param name="removed">The deleted profile.</param>
    public static ProfileTransition OnProfileRemoved(ProfileState state, ProfileId removed)
    {
        ArgumentNullException.ThrowIfNull(state);
        var corrected =
            state.LastProfile == removed ? state with { LastProfile = ProfileId.General } : state;
        return state.View is ViewTarget.Profile shown && shown.Id == removed
            ? Show(corrected, ProfileId.General, notice: null)
            : new ProfileTransition(corrected, ResetPage: false, Notice: null);
    }

    /// <summary>
    /// <paramref name="state"/> made consistent with <paramref name="library"/>: a view or a last profile that no longer
    /// exists becomes General (after an undo, an import or a restore).
    /// </summary>
    /// <param name="state">The profile state.</param>
    /// <param name="library">The shortcuts.</param>
    public static ProfileTransition Reconcile(ProfileState state, ShortcutLibrary library)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(library);
        var result = new ProfileTransition(state, ResetPage: false, Notice: null);
        if (state.LastProfile is { } last && !library.TryGetProfile(last, out _))
        {
            result = OnProfileRemoved(result.State, last);
        }

        return
            result.State.View is ViewTarget.Profile shown && !library.TryGetProfile(shown.Id, out _)
            ? OnProfileRemoved(result.State, shown.Id)
            : result;
    }

    private static ProfileTransition Show(ProfileState state, ProfileId id, ProfileNotice? notice)
    {
        var view = new ViewTarget.Profile(id);
        return new ProfileTransition(
            state with
            {
                View = view,
            },
            ResetPage: !state.View.Equals(view),
            notice
        );
    }
}
