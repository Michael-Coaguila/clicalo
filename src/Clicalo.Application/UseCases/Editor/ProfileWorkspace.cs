using Clicalo.Application.Confirmation;
using Clicalo.Application.Store;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Commands;
using Clicalo.Domain.Icons;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;

namespace Clicalo.Application.UseCases.Editor;

/// <summary>
/// The profile of the list in view, as «Atajos» edits it (ATJ-004 to ATJ-008): its name, icon and compatible mode, the
/// apps it follows and the capture mode. Every change is one undoable command (REG-07); deleting it needs two taps
/// (REG-04). Used by the Workspace role only.
/// </summary>
public sealed class ProfileWorkspace
{
    private readonly DocumentStore _store;
    private readonly ShortcutsWorkspace _shortcuts;
    private readonly Func<EditorCatalogs> _catalogs;

    /// <summary>Creates it.</summary>
    /// <param name="store">The document.</param>
    /// <param name="shortcuts">The section, whose list is the profile edited.</param>
    /// <param name="catalogs">The icon library, for the icon that follows the name.</param>
    public ProfileWorkspace(
        DocumentStore store,
        ShortcutsWorkspace shortcuts,
        Func<EditorCatalogs> catalogs
    )
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(shortcuts);
        ArgumentNullException.ThrowIfNull(catalogs);
        _store = store;
        _shortcuts = shortcuts;
        _catalogs = catalogs;
    }

    /// <summary>Raised after the capture mode or the pending question changed.</summary>
    public event EventHandler? Changed;

    /// <summary>A message for the status bar (CCM-003).</summary>
    public event EventHandler<WorkspaceNoticeEventArgs>? Noticed;

    /// <summary>The profile waiting for the next app in front (ATJ-008), or null.</summary>
    public ProfileId? Capturing { get; private set; }

    /// <summary>The question of ATJ-007 waiting for an answer, or null.</summary>
    public TakeOverRequest? PendingTakeOver { get; private set; }

    /// <summary>The profile of the list in view; null for Always visible.</summary>
    public Profile? Current =>
        _shortcuts.List is ListRef.InProfile inProfile
        && _store.Current.Library.TryGetProfile(inProfile.Id, out var profile)
            ? profile
            : null;

    /// <summary>
    /// The name of the profile (ATJ-004): the same in every language. An empty name is refused: the field goes back to
    /// the one it had.
    /// </summary>
    /// <param name="text">What the person typed or dictated.</param>
    /// <returns>Whether the name was accepted.</returns>
    public bool Rename(string text)
    {
        var name = (text ?? string.Empty).Trim();
        if (Current is not { } profile || name.Length == 0)
        {
            return false;
        }

        var languages = profile.Name.Values.Keys.Concat([LangCode.Es, LangCode.En]).Distinct();
        var renamed = profile with
        {
            Name = new LocalizedText(languages.Select(l => KeyValuePair.Create(l, name))),
        };
        if (profile.AutoIcon)
        {
            var suggested = IconSuggestions.Suggest(
                name,
                null,
                _store.Current.Settings.Keyboard.AppsLanguage,
                _catalogs().Icons,
                _catalogs().Combos
            );
            if (!suggested.IsEmpty)
            {
                renamed = renamed with { Icon = suggested[0] };
            }
        }

        return Edit(renamed, null);
    }

    /// <summary>An icon of the profile grid (ATJ-004): chosen by hand, so it no longer follows the name.</summary>
    /// <param name="icon">The icon.</param>
    public void SetIcon(IconRef icon)
    {
        if (Current is { } profile)
        {
            _ = Edit(profile with { Icon = icon, AutoIcon = false }, null);
        }
    }

    /// <summary>«Modo compatible» (ATJ-004, D24): scan codes for games and DirectInput.</summary>
    /// <param name="on">The switch.</param>
    public void SetCompatible(bool on)
    {
        if (Current is { } profile)
        {
            _ = Edit(
                profile with
                {
                    Injection = on ? InjectionMode.ScanCode : InjectionMode.VirtualKey,
                },
                null
            );
        }
    }

    /// <summary>[delProf], confirmed with two taps (ATJ-004, REG-04): the section goes to General (PER-008).</summary>
    /// <param name="token">The second tap.</param>
    public void Delete(ConfirmationToken token)
    {
        ArgumentNullException.ThrowIfNull(token);
        if (Current is not { } profile || profile.Id == ProfileId.General)
        {
            return;
        }

        _store.SealCoalescing();
        var result = _store.Dispatch(new DeleteProfile(profile.Id), token);
        if (result.IsFailure)
        {
            Warn(result.Failure.Message);
            return;
        }

        if (Capturing == profile.Id)
        {
            Capturing = null;
        }

        _shortcuts.SelectList(new ListRef.InProfile(ProfileId.General));
        Notify(L.ProfDeleted, "delete", undo: true);
        Raise();
    }

    /// <summary>
    /// A chip of [linkOpenApps] (ATJ-006): binds the process. If another profile has it, the question of ATJ-007 waits
    /// in <see cref="PendingTakeOver"/>.
    /// </summary>
    /// <param name="process">The process of the app.</param>
    public void Bind(ProcessName process)
    {
        if (Current is { } profile)
        {
            BindTo(profile.Id, process);
        }
    }

    /// <summary>The answer to the question of ATJ-007.</summary>
    /// <param name="yes">Whether to take the process from its owner.</param>
    public void AnswerTakeOver(bool yes)
    {
        if (PendingTakeOver is not { } pending)
        {
            return;
        }

        PendingTakeOver = null;
        if (yes)
        {
            _store.SealCoalescing();
            if (Dispatch(new BindProcess(pending.Profile, pending.Process, TakeOver: true)))
            {
                Notify(L.LinkedToApp(process: pending.Process.Value), "link", undo: true);
            }
        }

        Raise();
    }

    /// <summary>[linkNone] (ATJ-006): the profile is chosen by hand only, with [linkRemoved].</summary>
    public void Unlink()
    {
        if (Current is not { Binding: AppBinding.Processes } profile)
        {
            return;
        }

        _store.SealCoalescing();
        if (Edit(profile with { Binding = new AppBinding.Manual() }, null))
        {
            _store.SealCoalescing();
            Notify(L.LinkRemoved, "link_off", undo: true);
        }
    }

    /// <summary>[linkDetect] (ATJ-008): the next app in front will be bound, with the fixed notice [waitingApp].</summary>
    public void StartCapture()
    {
        if (Current is not { } profile || profile.Id == ProfileId.General)
        {
            return;
        }

        Capturing = profile.Id;
        PendingTakeOver = null;
        Notify(L.WaitingApp, "radar", undo: false);
        Raise();
    }

    /// <summary>Cancelar of the capture, from the Control Center or the panel (ATJ-008).</summary>
    public void CancelCapture()
    {
        if (Capturing is null)
        {
            return;
        }

        Capturing = null;
        Raise();
    }

    /// <summary>
    /// The verified external foreground changed (ATJ-008): while capturing, its process joins the processes of the
    /// waiting profile. The monitor already leaves out Clícalo's windows, the shell, the touch keyboard and Voice
    /// access; the caller leaves out the switches of «Probar ahora» (PRB-006).
    /// </summary>
    /// <param name="process">The process in front.</param>
    public void OnForeground(ProcessName process)
    {
        if (Capturing is not { } waiting || process.IsEmpty)
        {
            return;
        }

        Capturing = null;
        if (!_store.Current.Library.TryGetProfile(waiting, out _))
        {
            Raise();
            return;
        }

        BindTo(waiting, process);
        Raise();
    }

    /// <summary>The document changed: a capture or a question about a profile that is gone ends.</summary>
    public void OnDocumentChanged()
    {
        var library = _store.Current.Library;
        var changed = false;
        if (Capturing is { } waiting && !library.TryGetProfile(waiting, out _))
        {
            Capturing = null;
            changed = true;
        }

        if (PendingTakeOver is { } pending && !library.TryGetProfile(pending.Profile, out _))
        {
            PendingTakeOver = null;
            changed = true;
        }

        if (changed)
        {
            Raise();
        }
    }

    private void BindTo(ProfileId id, ProcessName process)
    {
        var owner = _store.Current.Library.ProfileFor(process);
        if (owner is not null && owner.Id != id)
        {
            PendingTakeOver = new TakeOverRequest(id, process, owner.Id);
            Raise();
            return;
        }

        if (owner?.Id == id)
        {
            return;
        }

        _store.SealCoalescing();
        if (Dispatch(new BindProcess(id, process, TakeOver: false)))
        {
            _store.SealCoalescing();
            Notify(L.LinkedToApp(process: process.Value), "link", undo: true);
        }
    }

    private bool Edit(Profile profile, Message? notice)
    {
        var ok = Dispatch(new EditProfile(profile));
        if (ok && notice is not null)
        {
            Notify(notice, "check", undo: true);
        }

        return ok;
    }

    private bool Dispatch(IDocumentCommand command)
    {
        var result = _store.Dispatch(command);
        if (result.IsFailure)
        {
            Warn(result.Failure.Message);
        }

        return result.IsSuccess;
    }

    private void Notify(Message text, string icon, bool undo) =>
        Noticed?.Invoke(
            this,
            new WorkspaceNoticeEventArgs(new WorkspaceNotice(text, icon, undo, false))
        );

    private void Warn(Message text) =>
        Noticed?.Invoke(
            this,
            new WorkspaceNoticeEventArgs(new WorkspaceNotice(text, "warning", false, true))
        );

    private void Raise() => Changed?.Invoke(this, EventArgs.Empty);
}
