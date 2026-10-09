using Clicalo.Application.Store;
using Clicalo.Domain.Commands;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.ProfileResolution;
using Clicalo.Domain.Settings;

namespace Clicalo.Application.Profiles;

/// <summary>
/// Which profile the panel shows (PER-001 to PER-008, docs/03 §2): it applies the pure rules of
/// <see cref="ProfileResolver"/> to the active app, the ★ Frequents button, the profile button, the selector and the
/// Auto/Fixed button, and keeps Auto/Fixed and the last profile in the settings (<c>lockProfile</c>,
/// <c>lastProfile</c>, not undoable: R-13). It decides nothing itself and raises <see cref="Changed"/> after every
/// change, with the page reset and the notice the rules give.
/// </summary>
/// <remarks>
/// It belongs to the Surfaces role: every member runs on the UI thread that owns the session (blueprint §3.2), so the
/// SysEvents reports of the foreground monitor are marshalled there before <see cref="OnActiveApp"/>. The active app is
/// the last real foreground process: the monitor already leaves out Clícalo's windows, the shell, the touch keyboard and
/// Voice access (PER-003). A report of the same app (coming back after the tray menu or the Control Center) is not a
/// change, so a manual choice lasts until the app really changes (PER-005).
/// </remarks>
public sealed class ProfileViewCoordinator
{
    private readonly DocumentStore _documents;

    /// <summary>
    /// Creates the coordinator on the document's settings: Auto or Fixed, and the view on the last profile when it
    /// still exists, or General (PER-001). The first active app reported then applies PER-003.
    /// </summary>
    /// <param name="documents">The document store, where the settings live.</param>
    public ProfileViewCoordinator(DocumentStore documents)
    {
        ArgumentNullException.ThrowIfNull(documents);
        _documents = documents;
        var settings = documents.Current.Settings;
        var library = documents.Current.Library;
        var first =
            settings.LastProfile is { } last && library.TryGetProfile(last, out _)
                ? last
                : ProfileId.General;
        State = new ProfileState(
            new ViewTarget.Profile(first),
            settings.LockProfile,
            settings.LastProfile
        );
    }

    /// <summary>Raised after every change of <see cref="State"/> or of the active app.</summary>
    public event EventHandler<ProfileViewChangedEventArgs>? Changed;

    /// <summary>The view, Auto/Fixed and the last profile.</summary>
    public ProfileState State { get; private set; }

    /// <summary>The active app, or <see langword="null"/> before the first one is known.</summary>
    public ProcessName? ActiveApp { get; private set; }

    /// <summary>The profile of the active app, or <see langword="null"/> when it has none (the «auto» dot, CAB-002).</summary>
    public ProfileId? ActiveAppProfile => ActiveApp is { } app ? Library.ProfileFor(app)?.Id : null;

    /// <summary>
    /// The profile the profile button names: the one shown, or retProf in Frequents (SEL-001, PER-004).
    /// </summary>
    public ProfileId ProfileButtonTarget =>
        State.View is ViewTarget.Profile shown
            ? shown.Id
            : ProfileResolver.ReturnProfile(State, Library, ActiveApp);

    private ShortcutLibrary Library => _documents.Current.Library;

    /// <summary>
    /// The foreground monitor reported <paramref name="app"/> (PER-003): when it is another app, Auto outside Frequents
    /// shows its profile or General; Fixed and Frequents stay.
    /// </summary>
    /// <param name="app">The process in front.</param>
    /// <returns>Whether the active app changed.</returns>
    public bool OnActiveApp(ProcessName app)
    {
        if (app.IsEmpty || ActiveApp is { } current && current == app)
        {
            return false;
        }

        ActiveApp = app;
        Apply(ProfileResolver.OnAppChanged(State, Library, app), appChanged: true);
        return true;
    }

    /// <summary>The ★ Frequents button.</summary>
    public void ShowFrequents() => Apply(ProfileResolver.ShowFrequents(State));

    /// <summary>
    /// The profile button while in Frequents: one tap back to retProf (PER-004, SEL-002). Outside Frequents the button
    /// opens the profile grid instead, and this does nothing.
    /// </summary>
    /// <returns>Whether the panel was in Frequents.</returns>
    public bool ReturnFromFrequents()
    {
        if (State.View is not ViewTarget.Frequents)
        {
            return false;
        }

        Apply(ProfileResolver.ReturnFromFrequents(State, Library, ActiveApp));
        return true;
    }

    /// <summary>A profile chosen in the profile grid (SEL-004): shown, and the last profile. Auto/Fixed stays.</summary>
    /// <param name="profile">The profile.</param>
    public void Choose(ProfileId profile) => Apply(ProfileResolver.Choose(State, Library, profile));

    /// <summary>The Auto/Fixed button or the «Follow the active app» switch, the same setting (PER-006).</summary>
    public void ToggleLock() => Apply(ProfileResolver.ToggleLock(State, Library, ActiveApp));

    /// <summary>A template of the active app was installed as <paramref name="profile"/> (PER-007).</summary>
    /// <param name="profile">The new profile.</param>
    public void OnTemplateInstalled(ProfileId profile) =>
        Apply(ProfileResolver.OnTemplateInstalled(State, profile));

    /// <summary>
    /// The document changed (a deleted profile, an undo, an import, a restore, or Auto/Fixed changed elsewhere): the
    /// state follows the settings and a view or a last profile that no longer exists becomes General (PER-008).
    /// </summary>
    public void OnDocumentChanged()
    {
        var settings = _documents.Current.Settings;
        var synced = State with
        {
            LockProfile = settings.LockProfile,
            LastProfile = settings.LastProfile,
        };
        Apply(ProfileResolver.Reconcile(synced, Library), previous: State);
    }

    private void Apply(
        ProfileTransition transition,
        bool appChanged = false,
        ProfileState? previous = null
    )
    {
        var before = previous ?? State;
        var after = transition.State;
        if (after == before && transition.Notice is null && !appChanged)
        {
            return;
        }

        State = after;
        Persist(after);
        Changed?.Invoke(
            this,
            new ProfileViewChangedEventArgs(
                before,
                after,
                transition.ResetPage,
                transition.Notice,
                appChanged
            )
        );
    }

    private void Persist(ProfileState state)
    {
        var settings = _documents.Current.Settings;
        if (settings.LockProfile != state.LockProfile)
        {
            _ = _documents.Dispatch(new SetSetting(SettingPaths.LockProfile, state.LockProfile));
        }

        if (settings.LastProfile != state.LastProfile)
        {
            _ = _documents.Dispatch(new SetSetting(SettingPaths.LastProfile, state.LastProfile));
        }
    }
}
