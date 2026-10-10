using System.Collections.Immutable;
using System.Globalization;
using Clicalo.Application.Confirmation;
using Clicalo.Application.Ports;
using Clicalo.Application.UseCases.Editor;
using Clicalo.Application.UseCases.Library;
using Clicalo.Domain.Duplicates;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.VoiceNumbering;
using Clicalo.Presentation.ControlCenter.Editor;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clicalo.Presentation.ControlCenter.Shortcuts;

/// <summary>
/// The «Atajos» section of the Control Center (docs/05 §1, ATJ-001 to ATJ-011): the profiles, the shortcuts grid, the
/// library and the editor, projected from <see cref="ShortcutsWorkspace"/> and <see cref="ProfileWorkspace"/> into
/// <see cref="Screen"/> and <see cref="Editor"/>. It forwards every intention; the rules are the Application's.
/// Projections are coalesced to one per dispatcher turn.
/// </summary>
public sealed class ShortcutsSectionViewModel : ObservableObject
{
    private readonly ControlCenterServices _s;
    private ShortcutsScreen _screen;
    private bool _queued;
    private bool _profileEditOpen;
    private bool _linkOpen;
    private ImmutableArray<OpenApp> _apps = [];

    /// <summary>Creates the section and projects it.</summary>
    /// <param name="services">The services of the Control Center.</param>
    public ShortcutsSectionViewModel(ControlCenterServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _s = services;
        Editor = new ShortcutEditorViewModel(services, Invalidate);
        _screen = Build();
        Editor.Project();
    }

    /// <summary>Everything outside the editor.</summary>
    public ShortcutsScreen Screen
    {
        get => _screen;
        private set => SetProperty(ref _screen, value);
    }

    /// <summary>The editor of the shortcut in view.</summary>
    public ShortcutEditorViewModel Editor { get; }

    /// <summary>The count of the side menu: distinct repeated combinations (REP-003).</summary>
    public int RepeatedCount => _s.Shortcuts.Duplicates().RepeatedCombinationCount;

    private ShortcutsWorkspace Workspace => _s.Shortcuts;

    private ProfileWorkspace Profiles => _s.Profiles;

    private LangCode Language => new(_s.Localization.Current.Locale.Code);

    /// <summary>Projects again once the current work of the dispatcher is done.</summary>
    public void Invalidate()
    {
        if (_queued)
        {
            return;
        }

        _queued = true;
        _s.Post(Refresh);
    }

    /// <summary>Projects the section and the editor now.</summary>
    public void Refresh()
    {
        _queued = false;
        Screen = Build();
        Editor.Project();
        OnPropertyChanged(nameof(RepeatedCount));
    }

    /// <summary>Esc with a menu open (CCM-001): closes the innermost one and says whether there was one.</summary>
    public bool CloseMenu()
    {
        if (Editor.CloseMenu())
        {
            return true;
        }

        if (Profiles.PendingTakeOver is not null)
        {
            Profiles.AnswerTakeOver(yes: false);
            return true;
        }

        if (_linkOpen)
        {
            _linkOpen = false;
        }
        else if (_profileEditOpen)
        {
            _profileEditOpen = false;
        }
        else if (Workspace.Pane is EditorPane.Library)
        {
            Workspace.CloseLibrary();
            return true;
        }
        else
        {
            return false;
        }

        Invalidate();
        return true;
    }

    /// <summary>A row of the profiles column (ATJ-002).</summary>
    /// <param name="list">The list.</param>
    public void SelectList(ListRef list)
    {
        _profileEditOpen = false;
        _linkOpen = false;
        Workspace.SelectList(list);
    }

    /// <summary>«+ Nuevo perfil» (ATJ-002): the Plantillas section.</summary>
    public void NewProfile() => _s.OpenTemplates();

    /// <summary>✏ of the header, or [done] of the card (ATJ-004).</summary>
    public void ToggleProfileEdit()
    {
        _profileEditOpen = !_profileEditOpen;
        _s.Confirm.Disarm();
        Invalidate();
    }

    /// <summary>The name field of the profile card (ATJ-004); an empty name goes back to the one it had.</summary>
    /// <param name="text">The name.</param>
    /// <returns>Whether the name was accepted.</returns>
    public bool RenameProfile(string text)
    {
        var accepted = Profiles.Rename(text);
        Invalidate();
        return accepted;
    }

    /// <summary>An icon of the profile card (ATJ-004).</summary>
    /// <param name="icon">The icon.</param>
    public void SetProfileIcon(string icon) => Profiles.SetIcon(new(icon));

    /// <summary>The «Modo compatible» row (ATJ-004).</summary>
    public void ToggleCompatible() =>
        Profiles.SetCompatible(Profiles.Current?.Injection != InjectionMode.ScanCode);

    /// <summary>[delProf]: two taps (ATJ-004, REG-04).</summary>
    public void DeleteProfile()
    {
        if (Profiles.Current is not { } profile || profile.Id == ProfileId.General)
        {
            return;
        }

        switch (_s.Confirm.Tap(new ConfirmationSubject(nameof(DeleteProfile), profile.Id.Value)))
        {
            case TwoStepResult.Confirmed confirmed:
                _profileEditOpen = false;
                Profiles.Delete(confirmed.Token);
                break;
            case TwoStepResult.Armed armed:
                _ = _s.Time.CreateTimer(
                    _ => _s.Post(Invalidate),
                    null,
                    armed.Until - _s.Time.GetUtcNow(),
                    Timeout.InfiniteTimeSpan
                );
                break;
        }

        Invalidate();
    }

    /// <summary>
    /// [Compartir] of the profile card (DAT-007): saves <c>clicalo-perfil-&lt;id&gt;.json</c> where the person chooses.
    /// The texts are left out, with a notice, unless the person chose [shareWithTexts] (PQ-37).
    /// </summary>
    /// <param name="withTexts">Whether to include the texts in clear.</param>
    public async Task ShareAsync(bool withTexts)
    {
        if (_s.Templates is not { } templates || Profiles.Current is not { } profile)
        {
            return;
        }

        var file = templates.Sharing.Export(profile, withTexts);
        var saved = await templates
            .SaveShare(file.FileName, file.Content, CancellationToken.None)
            .ConfigureAwait(true);
        if (saved is null)
        {
            return;
        }

        templates.Notify(
            saved.Value
                ? new WorkspaceNotice(
                    file.ExcludedTexts > 0
                        ? L.SharedTextsExcluded(file.ExcludedTexts)
                        : L.ProfShared(name: file.FileName),
                    "share",
                    false,
                    file.ExcludedTexts > 0
                )
                : new WorkspaceNotice(L.SaveFailT, "warning", false, true)
        );
    }

    /// <summary>🎤 of a field of the section (ACC-011).</summary>
    public void Dictate() => Editor.Dictate();

    /// <summary>The binding row (ATJ-005): Cancelar while waiting; otherwise its options unfold.</summary>
    public void LinkAction()
    {
        if (Profiles.Capturing is not null)
        {
            Profiles.CancelCapture();
            return;
        }

        _linkOpen = !_linkOpen;
        if (_linkOpen)
        {
            _ = LoadAppsAsync();
        }

        Invalidate();
    }

    /// <summary>A chip of [linkOpenApps] (ATJ-006).</summary>
    /// <param name="process">The process.</param>
    public void BindApp(string process) => Profiles.Bind(new ProcessName(process));

    /// <summary>[linkDetect] (ATJ-008).</summary>
    public void Detect()
    {
        _linkOpen = false;
        Profiles.StartCapture();
    }

    /// <summary>[linkNone] (ATJ-006).</summary>
    public void Unlink()
    {
        _linkOpen = false;
        Profiles.Unlink();
    }

    /// <summary>The answer to [processTaken] (ATJ-007).</summary>
    /// <param name="yes">Whether to move the process.</param>
    public void AnswerTakeOver(bool yes) => Profiles.AnswerTakeOver(yes);

    /// <summary>A tile of the grid (ATJ-009).</summary>
    /// <param name="id">The shortcut.</param>
    public void SelectTile(ShortcutId id) => Workspace.Select(id);

    /// <summary>A tile dropped on another, or at the end (ATJ-009).</summary>
    /// <param name="moved">The dragged tile.</param>
    /// <param name="before">The tile it was dropped on; null for the end.</param>
    public void Reorder(ShortcutId moved, ShortcutId? before) => Workspace.Reorder(moved, before);

    /// <summary>«+ Añadir» and the «Biblioteca» tile (ATJ-003, ATJ-010).</summary>
    public void OpenLibrary() => Workspace.OpenLibrary();

    /// <summary>✕ of «Añadir atajo».</summary>
    public void CloseLibrary() => Workspace.CloseLibrary();

    /// <summary>A category chip (ATJ-010).</summary>
    /// <param name="id">The category.</param>
    public void ChooseCategory(string id) => Workspace.ChooseCategory(id);

    /// <summary>«Crear el mío» (ATJ-010).</summary>
    public void CreateOwn() => Workspace.CreateOwn();

    /// <summary>A row of the library (ATJ-010).</summary>
    /// <param name="index">The row.</param>
    public void AddFromLibrary(int index)
    {
        if (CurrentCategory() is { } category && index >= 0 && index < category.Items.Count)
        {
            Workspace.AddFromLibrary(category, category.Items[index]);
        }
    }

    /// <summary>The chip «N combinaciones repetidas · Revisar» (REP-003).</summary>
    public void ReviewDuplicates() => Workspace.ReviewDuplicates();

    /// <summary>The Control Center opens: what was unfolded closes.</summary>
    public void OnOpened()
    {
        _profileEditOpen = false;
        _linkOpen = false;
        Invalidate();
    }

    private static ImmutableArray<ProcessName> ProcessesOf(AppBinding binding) =>
        binding is AppBinding.Processes bound ? bound.Names.Items : [];

    private static string DisplayName(ProcessName process) =>
        process.Value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? process.Value[..^4]
            : process.Value;

    private string T(Message message) => _s.Localization.Current.Format(message);

    private static bool HasTexts(Profile profile) =>
        profile.Shortcuts.Items.Any(s =>
            s.Action is TextAction
            || (s.Action is MacroAction macro && macro.Steps.Items.Any(step => step is TextStep))
        );

    private async Task LoadAppsAsync()
    {
        var apps = await _s.OpenApps(CancellationToken.None).ConfigureAwait(true);
        _s.Post(() =>
        {
            _apps = apps;
            Editor.ApplyApps(apps);
            Invalidate();
        });
    }

    private LibraryCategory? CurrentCategory()
    {
        var categories = Workspace.Categories();
        return categories.FirstOrDefault(c =>
                string.Equals(c.Id, Workspace.LibraryCategory, StringComparison.Ordinal)
            ) ?? (categories.Count > 0 ? categories[0] : null);
    }

    private ShortcutsScreen Build()
    {
        var document = _s.Store.Current;
        var library = document.Library;
        var duplicates = Workspace.Duplicates();
        var profile = Profiles.Current;
        if (profile is null)
        {
            _profileEditOpen = false;
        }

        return new ShortcutsScreen(
            TopBar(library, duplicates),
            new ProfilesModel(
                T(L.Profiles),
                [
                    new ProfileRow(
                        new ListRef.AlwaysVisible(),
                        "push_pin",
                        T(L.Always),
                        T(L.AllApps),
                        Workspace.List is ListRef.AlwaysVisible
                    ),
                    .. library.Profiles.Select(p => new ProfileRow(
                        new ListRef.InProfile(p.Id),
                        p.Icon.Name,
                        p.Name.Get(Language, LangCode.Es),
                        ProcessesOf(p.Binding) is { IsEmpty: false } processes
                            ? string.Join(", ", processes.Select(n => n.Value))
                            : T(L.NoProcess),
                        Workspace.List is ListRef.InProfile shown && shown.Id == p.Id
                    )),
                ],
                "+ " + T(L.NewProfile)
            ),
            Header(profile),
            _profileEditOpen && profile is not null ? ProfileEdit(profile) : null,
            profile is null ? null : Link(profile),
            Grid(duplicates, document.Settings.VoiceNumbers),
            Workspace.Pane switch
            {
                EditorPane.Library => EditorColumn.Library,
                EditorPane.Editing or EditorPane.Draft => EditorColumn.Editor,
                _ => EditorColumn.Empty,
            },
            T(L.PickOne),
            Workspace.Pane is EditorPane.Library ? Library() : null
        );
    }

    private TopBarModel TopBar(ShortcutLibrary library, DuplicateIndex duplicates)
    {
        var active = _s.ActiveAppProfile();
        var count = duplicates.RepeatedCombinationCount;
        return new TopBarModel(
            T(L.PanelShows),
            [
                new LayerChip(
                    "push_pin",
                    T(L.Always),
                    library.AlwaysVisible.Count.ToString(CultureInfo.InvariantCulture)
                ),
                new LayerChip(
                    active?.Icon.Name ?? "apps",
                    active?.Name.Get(Language, LangCode.Es) ?? T(L.NoProf),
                    T(L.ActiveCount(active?.Shortcuts.Count ?? 0))
                ),
                new LayerChip("star", T(L.Freq), T(L.AutoW)),
            ],
            count > 0 ? T(L.DupReviewN(count)) : null
        );
    }

    private ListHeaderModel Header(Profile? profile)
    {
        if (profile is null)
        {
            return new ListHeaderModel(
                "push_pin",
                T(L.Always),
                T(L.GlobalSub),
                false,
                false,
                T(L.EditProf),
                T(L.Add)
            );
        }

        var processes = ProcessesOf(profile.Binding).ToList();
        return new ListHeaderModel(
            profile.Icon.Name,
            profile.Name.Get(Language, LangCode.Es),
            processes.Count > 0
                ? T(L.OpensWithApps(process: string.Join(", ", processes.Select(p => p.Value))))
                : T(L.ManualSub),
            true,
            _profileEditOpen,
            T(L.EditProf),
            T(L.Add)
        );
    }

    private ProfileEditModel ProfileEdit(Profile profile)
    {
        var armed =
            _s.Confirm.ArmedSubject is { } subject
            && string.Equals(subject.Operation, nameof(DeleteProfile), StringComparison.Ordinal)
            && string.Equals(subject.Target, profile.Id.Value, StringComparison.Ordinal);
        var icons = new List<string> { profile.Icon.Name };
        icons.AddRange(
            _s.Catalogs()
                .Icons.ProfileFeatured.Select(i => i.Name)
                .Where(i => !string.Equals(i, profile.Icon.Name, StringComparison.Ordinal))
        );
        return new ProfileEditModel(
            T(L.ProfName),
            profile.Name.Get(Language, LangCode.Es),
            T(L.SearchDictate),
            T(L.Icon),
            [
                .. icons.Select(i => new IconOption(
                    i,
                    string.Equals(i, profile.Icon.Name, StringComparison.Ordinal)
                )),
            ],
            T(L.CompatT),
            T(L.CompatD),
            profile.Injection == InjectionMode.ScanCode,
            T(L.ShareProf),
            T(L.Done),
            profile.Id == ProfileId.General ? null : T(armed ? L.DelConfirm : L.DelProf),
            armed,
            _s.Templates is not null,
            _s.Templates is not null && HasTexts(profile) ? T(L.ShareWithTexts) : null
        );
    }

    private LinkModel Link(Profile profile)
    {
        var processes = ProcessesOf(profile.Binding).ToList();
        var question =
            Profiles.PendingTakeOver is { } pending && pending.Profile == profile.Id
                ? T(
                    L.ProcessTaken(
                        process: pending.Process.Value,
                        profile: _s.Store.Current.Library.TryGetProfile(
                            pending.Owner,
                            out var owner
                        )
                            ? owner.Name.Get(Language, LangCode.Es)
                            : string.Empty
                    )
                )
                : null;
        var (state, icon, title, subtitle, action) = profile switch
        {
            { Id: var id } when id == ProfileId.General => (
                LinkState.General,
                "apps",
                T(L.GenLinkT),
                T(L.GenLinkS),
                (string?)null
            ),
            _ when Profiles.Capturing == profile.Id => (
                LinkState.Waiting,
                "radar",
                T(L.WaitingT),
                T(L.WaitingS),
                T(L.Cancel)
            ),
            _ when processes.Count > 0 => (
                LinkState.Linked,
                "link",
                T(L.LinkedT(app: string.Join(", ", processes.Select(Name)))),
                string.Join(" · ", processes.Select(p => p.Value)),
                T(L.Change)
            ),
            _ => (LinkState.Unlinked, "link_off", T(L.UnlinkedT), T(L.UnlinkedS), T(L.LinkBtn)),
        };
        return new LinkModel(
            state,
            icon,
            title,
            subtitle,
            action,
            _linkOpen && state != LinkState.General && state != LinkState.Waiting,
            T(L.LinkOpenApps),
            [
                .. _apps.Select(a => new AppChip(
                    a.Process.Value,
                    a.Name,
                    processes.Contains(a.Process)
                )),
            ],
            T(L.LinkDetect),
            T(L.LinkNone),
            question,
            T(L.TakeOverYes),
            T(L.Cancel)
        );

        // «Se activa solo con Word»: the open app's name, or the profile's when it follows a single app.
        string Name(ProcessName process) =>
            _apps.FirstOrDefault(a => a.Process == process)?.Name
            ?? (
                processes.Count == 1
                    ? profile.Name.Get(Language, LangCode.Es)
                    : DisplayName(process)
            );
    }

    private GridModel Grid(DuplicateIndex duplicates, bool numbers)
    {
        var shortcuts = Workspace.Shortcuts;
        var selected = Workspace.Pane is EditorPane.Editing editing
            ? editing.Id
            : (ShortcutId?)null;
        var labels = _s.Catalogs().KeyLabels;
        return new GridModel(
            [
                .. shortcuts.Select(
                    (shortcut, i) =>
                    {
                        var name = shortcut.Name.Get(Language, LangCode.Es);
                        var shown = name.Length == 0 ? T(L.NamePh2) : name;
                        var number = VoiceNumbers.ForList(i);
                        var repeated = duplicates.IsRepeated(shortcut.Id);
                        var incomplete =
                            ShortcutCompleteness.Evaluate(shortcut) != CompletenessIssue.None;
                        var state = string.Join(
                            ", ",
                            new[]
                            {
                                repeated ? T(L.DupTitle) : null,
                                incomplete ? T(L.Incomplete) : null,
                            }.OfType<string>()
                        );
                        return new GridTile(
                            shortcut.Id,
                            shortcut.Icon.Name,
                            shortcut.Category.Value,
                            shown,
                            Foot(shortcut, labels),
                            repeated,
                            shortcut.Action.Kind == ActionKind.Tap
                                ? null
                                : TypeIcon(shortcut.Action.Kind),
                            numbers ? number.ToString(CultureInfo.InvariantCulture) : null,
                            incomplete,
                            selected == shortcut.Id && Workspace.Pane is not EditorPane.Library,
                            numbers ? VoiceNumbers.Prefix(number, shown) : shown,
                            state
                        );
                    }
                ),
            ],
            T(L.FromLib),
            T(L.Incomplete),
            T(L.OrderHintDrag)
        );
    }

    private static string TypeIcon(ActionKind kind) =>
        kind switch
        {
            ActionKind.Hold => "pan_tool",
            ActionKind.Toggle => "toggle_on",
            ActionKind.Text => "text_fields",
            ActionKind.Mouse => "mouse",
            ActionKind.Macro => "playlist_play",
            ActionKind.Url => "public",
            ActionKind.App => "open_in_new",
            ActionKind.System => "settings",
            _ => "touch_app",
        };

    private string Foot(Shortcut shortcut, KeyLabelCatalog labels) =>
        shortcut.Action switch
        {
            MacroAction macro => T(L.StepsN(macro.Steps.Count)),
            MouseAction mouse => _s.Catalogs()
                .MouseActions.Items.FirstOrDefault(m => m.Op == mouse.Op)
                ?.Label.Get(Language, LangCode.Es)
                ?? string.Empty,
            { } action when ActionKinds.ChordOf(action) is { } chord => KeyChordFormatter.Format(
                chord,
                labels,
                KeyLabelStyle.Full,
                Language,
                LangCode.Es
            ),
            _ => string.Empty,
        };

    private LibraryModel Library()
    {
        var categories = Workspace.Categories();
        var current = CurrentCategory();
        var labels = _s.Catalogs().KeyLabels;
        var list = Workspace.Shortcuts;
        return new LibraryModel(
            T(L.AddTitle),
            T(L.Close),
            T(L.CreateOwn),
            T(L.CreateOwnD),
            T(L.OrPick),
            [
                .. categories.Select(c => new LibraryChip(
                    c.Id,
                    c.Icon,
                    T(c.Label),
                    current is not null && string.Equals(c.Id, current.Id, StringComparison.Ordinal)
                )),
            ],
            current is null
                ? []
                :
                [
                    .. current.Items.Select(
                        (item, i) =>
                        {
                            var added = LibraryMatching.IsAdded(list, item, current.Source);
                            var chord = ActionKinds.ChordOf(item.Action);
                            return new LibraryRow(
                                i,
                                item.Icon.Name,
                                item.Category.Value,
                                item.Name.Get(Language, LangCode.Es),
                                chord is null
                                    ? string.Empty
                                    : KeyChordFormatter.Format(
                                        chord,
                                        labels,
                                        KeyLabelStyle.Full,
                                        Language,
                                        LangCode.Es
                                    ),
                                added,
                                added ? T(L.AlreadyAdded) : string.Empty
                            );
                        }
                    ),
                ]
        );
    }
}
