using System.Collections.Immutable;
using System.Globalization;
using Clicalo.Application.Confirmation;
using Clicalo.Application.Ports;
using Clicalo.Application.UseCases.Ai;
using Clicalo.Application.UseCases.Editor;
using Clicalo.Application.UseCases.Templates;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Commands;
using Clicalo.Domain.Icons;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Privacy;
using Clicalo.Domain.Settings;
using Clicalo.Domain.Templates;
using Clicalo.Presentation.ControlCenter.Shortcuts;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clicalo.Presentation.ControlCenter.Templates;

/// <summary>
/// The section Plantillas of the Control Center (docs/05 §2, PLA-001 to PLA-017): «Crear con IA», «Perfil vacío»,
/// «Apps abiertas sin perfil», «Plantillas disponibles», «Tus perfiles» and the preview, projected into
/// <see cref="Screen"/>. It forwards every intention to <see cref="TemplatePreviewSession"/>,
/// <see cref="AiAssistant"/> and the document; the rules are the Application's and the Domain's.
/// </summary>
public sealed class TemplatesSectionViewModel : ObservableObject
{
    private const string Detect = "detect";
    private const string NoLink = "none";
    private const string AiKeySubject = "ai-key";

    private readonly ControlCenterServices _s;
    private readonly TemplatesServices _t;
    private readonly Action<ProfileId, bool> _openProfile;
    private TemplatesScreen _screen;
    private ImmutableArray<OpenApp> _apps = [];
    private string _query = string.Empty;
    private string _blankName = string.Empty;
    private string? _blankIcon;
    private string _blankLink = NoLink;
    private string? _selectedTemplate;
    private int? _editingRow;
    private bool _keyOpen;
    private bool _keyboardOpen;
    private bool _blankOpen;
    private bool _blankIconsOpen;
    private bool _hasKey;
    private bool _queued;

    /// <summary>Creates the section and projects it.</summary>
    /// <param name="services">The services of the Control Center.</param>
    /// <param name="templates">The services of Plantillas.</param>
    /// <param name="openProfile">Opens «Atajos» on a profile, with the library when the flag is set.</param>
    public TemplatesSectionViewModel(
        ControlCenterServices services,
        TemplatesServices templates,
        Action<ProfileId, bool> openProfile
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(openProfile);
        _s = services;
        _t = templates;
        _openProfile = openProfile;
        _screen = Build();
    }

    /// <summary>Everything the section shows.</summary>
    public TemplatesScreen Screen
    {
        get => _screen;
        private set => SetProperty(ref _screen, value);
    }

    private LangCode Language => new(_s.Localization.Current.Locale.Code);

    private UserSettings Settings => _s.Store.Current.Settings;

    private StarterContent? Content => _s.Catalogs().Starter;

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

    /// <summary>Projects the section now.</summary>
    public void Refresh()
    {
        _queued = false;
        Screen = Build();
    }

    /// <summary>The section opens: the open apps are read again and the key state is checked.</summary>
    public void OnOpened()
    {
        _hasKey = _t.Ai.HasKey;
        _ = LoadAppsAsync();
        Invalidate();
    }

    /// <summary>Esc with something unfolded (CCM-001): closes the innermost one and says whether there was one.</summary>
    public bool CloseMenu()
    {
        if (_editingRow is not null)
        {
            _editingRow = null;
        }
        else if (_keyOpen)
        {
            _keyOpen = false;
        }
        else if (_keyboardOpen)
        {
            _keyboardOpen = false;
        }
        else if (_blankIconsOpen)
        {
            _blankIconsOpen = false;
        }
        else
        {
            return false;
        }

        Invalidate();
        return true;
    }

    // ---- Crear con IA ---------------------------------------------------------------------------------------

    /// <summary>The program field (PLA-002).</summary>
    /// <param name="text">What the person typed or dictated.</param>
    public void SetQuery(string text)
    {
        _query = text ?? string.Empty;
        Invalidate();
    }

    /// <summary>An example chip: only fills the field (PLA-002).</summary>
    /// <param name="name">The program name.</param>
    public void UseExample(string name) => SetQuery(name);

    /// <summary>🎤 of a field (ACC-011): the view focuses the field first.</summary>
    public void Dictate() => _ = DictateAsync();

    /// <summary>[Generar con IA] (PLA-005).</summary>
    public Task GenerateAsync() => RunAsync(_query);

    /// <summary>[Usar mi clave] / [Cambiar clave] (PLA-003).</summary>
    public void ToggleKey()
    {
        _keyOpen = !_keyOpen;
        Invalidate();
    }

    /// <summary>[Listo] of the key field: saves the pasted key, which is never shown again (PLA-003, ADR-0008).</summary>
    /// <param name="key">The key in the hidden field; empty closes the field.</param>
    public void SaveKey(string key)
    {
        _keyOpen = false;
        if (!string.IsNullOrWhiteSpace(key))
        {
            var saved = _t.Ai.SaveKey(new Sensitive<string>(key, RedactionKind.Secret));
            _hasKey = _t.Ai.HasKey;
            Notify(saved ? L.KeySaved : L.KeyNotSaved, saved ? "key" : "warning", !saved);
        }

        Invalidate();
    }

    /// <summary>
    /// [keyDelete]: two taps (REG-04, [delConfirm] while armed), then the saved key goes away (ADR-0008). It cannot be
    /// undone because Clícalo never keeps a copy of the key; pasting it again restores it.
    /// </summary>
    public void DeleteKey()
    {
        switch (_s.Confirm.Tap(new ConfirmationSubject(nameof(DeleteKey), AiKeySubject)))
        {
            case TwoStepResult.Confirmed:
                _t.Ai.DeleteKey();
                _hasKey = _t.Ai.HasKey;
                _keyOpen = false;
                Notify(L.KeyDeleted, "key_off", false);
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

    private bool DeleteKeyArmed =>
        _s.Confirm.ArmedSubject is { } subject
        && string.Equals(subject.Operation, nameof(DeleteKey), StringComparison.Ordinal)
        && string.Equals(subject.Target, AiKeySubject, StringComparison.Ordinal);

    /// <summary>[Aceptar y generar] (PLA-004).</summary>
    public Task AcceptConsentAsync()
    {
        _t.Ai.AcceptConsent();
        return RunAsync(_t.Ai.LastApp);
    }

    /// <summary>[No usar IA] (PLA-004).</summary>
    public void DeclineConsent()
    {
        _t.Ai.Decline();
        Invalidate();
    }

    /// <summary>A way out of the error card (PLA-006).</summary>
    /// <param name="kind">Which one.</param>
    public Task ErrorActionAsync(ErrorActionKind kind)
    {
        switch (kind)
        {
            case ErrorActionKind.Retry:
                return RunAsync(_t.Ai.LastApp);
            case ErrorActionKind.Key:
                _t.Ai.Dismiss();
                _keyOpen = true;
                break;
            case ErrorActionKind.Enable:
                _t.Ai.Enable();
                break;
            case ErrorActionKind.Blank:
                _blankName = _t.Ai.LastApp;
                _blankOpen = true;
                _t.Ai.Dismiss();
                break;
            default:
                _t.Ai.Dismiss();
                break;
        }

        Invalidate();
        return Task.CompletedTask;
    }

    /// <summary>The keyboard line: [change] / [done] (PLA-009).</summary>
    public void ToggleKeyboard()
    {
        _keyboardOpen = !_keyboardOpen;
        Invalidate();
    }

    /// <summary>A layout of the keyboard line (PLA-009).</summary>
    /// <param name="id">The layout.</param>
    public void SetLayout(string id)
    {
        if (KeyboardLayouts.All.Items.Contains(id, StringComparer.Ordinal))
        {
            _ = _s.Store.Dispatch(new SetSetting(SettingPaths.KeyboardLayout, id));
        }
    }

    /// <summary>A programs language of the keyboard line: it decides which variant installs (PLA-009).</summary>
    /// <param name="code">The language code.</param>
    public void SetAppsLanguage(string code) =>
        _ = _s.Store.Dispatch(new SetSetting(SettingPaths.AppsLanguage, new LangCode(code)));

    // ---- Perfil vacío ---------------------------------------------------------------------------------------

    /// <summary>The header of «Perfil vacío» (PLA-010, ExpandCollapse).</summary>
    public void ToggleBlank()
    {
        _blankOpen = !_blankOpen;
        if (_blankOpen)
        {
            _ = LoadAppsAsync();
        }

        Invalidate();
    }

    /// <summary>The name of the empty profile; the icon follows it until one is chosen.</summary>
    /// <param name="text">What the person typed or dictated.</param>
    public void SetBlankName(string text)
    {
        _blankName = text ?? string.Empty;
        Invalidate();
    }

    /// <summary>The icon with ✏: opens or closes the grid.</summary>
    public void ToggleBlankIcons()
    {
        _blankIconsOpen = !_blankIconsOpen;
        Invalidate();
    }

    /// <summary>An icon of the grid: chosen by hand, it no longer follows the name.</summary>
    /// <param name="icon">The icon.</param>
    public void SetBlankIcon(string icon)
    {
        _blankIcon = icon;
        _blankIconsOpen = false;
        Invalidate();
    }

    /// <summary>An option of «Se activa con».</summary>
    /// <param name="id">An open app's process, <c>detect</c> or <c>none</c>.</param>
    public void SetBlankLink(string id)
    {
        _blankLink = id;
        Invalidate();
    }

    /// <summary>[blankCreate] (PLA-010): creates it and opens «Atajos» with the library.</summary>
    public void CreateBlank()
    {
        var process = _blankLink is Detect or NoLink
            ? (ProcessName?)null
            : new ProcessName(_blankLink);
        var created = BlankProfiles.Create(
            _s.Store,
            _blankName,
            new IconRef(BlankIcon()),
            _blankIcon is null,
            process
        );
        if (!created.TryGetValue(out var id))
        {
            return;
        }

        var link = _blankLink;
        _blankName = string.Empty;
        _blankIcon = null;
        _blankLink = NoLink;
        _blankOpen = false;
        Notify(L.ProfCreated, "add", false, undo: true);
        Open(id, string.Equals(link, Detect, StringComparison.Ordinal), process);
    }

    /// <summary>[unknownBlank] (PLA-007): the empty profile with the program name, waiting for the app.</summary>
    public void CreateFromUnknown()
    {
        var name = _t.Preview.Draft()?.Name.Get(Language, LangCode.Es) ?? _t.Ai.LastApp;
        var created = BlankProfiles.Create(_s.Store, name, IconCatalog.ProfileDefault, true, null);
        if (created.TryGetValue(out var id))
        {
            _t.Preview.Clear();
            Notify(L.ProfCreated, "add", false, undo: true);
            Open(id, capture: true, process: null);
        }
    }

    // ---- Apps abiertas, plantillas y perfiles ---------------------------------------------------------------

    /// <summary>The «Detectar» switch (PLA-011): the same setting as the panel's suggestion.</summary>
    public void ToggleDetect() =>
        _ = _s.Store.Dispatch(
            new SetSetting(SettingPaths.AutoSuggestProfiles, !Settings.AutoSuggestProfiles)
        );

    /// <summary>A template card or [Vista previa] (PLA-011, PLA-012).</summary>
    /// <param name="id">The template.</param>
    public void PreviewTemplate(string id)
    {
        if (Content?.Template(id) is not { } template)
        {
            return;
        }

        _t.Preview.ShowTemplate(template);
        _selectedTemplate = id;
        _editingRow = null;
        Invalidate();
    }

    /// <summary>[Instalar] of a card (PLA-011, PLA-012): all of it, without preview, with undo.</summary>
    /// <param name="id">The template.</param>
    public void InstallTemplate(string id)
    {
        if (Content?.Template(id) is not { } template)
        {
            return;
        }

        if (_t.Preview.InstallAll(template, Language).TryGetValue(out var outcome))
        {
            if (string.Equals(_selectedTemplate, id, StringComparison.Ordinal))
            {
                _selectedTemplate = null;
            }

            Announce(outcome);
        }

        Invalidate();
    }

    /// <summary>A button of «Tus perfiles» (PLA-014): «Atajos» on that profile.</summary>
    /// <param name="id">The profile.</param>
    public void OpenInstalled(ProfileId id) => _openProfile(id, false);

    /// <summary>[importProf] (PLA-014, DAT-007): the file opens in the preview without installing ([importedPv]).</summary>
    public async Task ImportAsync()
    {
        var bytes = await _t.PickImport(CancellationToken.None).ConfigureAwait(true);
        if (bytes is not { } content)
        {
            return;
        }

        var read = _t.Sharing.Import(content);
        if (read.TryGetValue(out var shared))
        {
            _t.Preview.ShowShared(shared);
            _selectedTemplate = null;
            _editingRow = null;
            Notify(L.ImportedPv, "download", false);
        }
        else
        {
            Notify(read.Failure.Message, "warning", true);
        }

        Invalidate();
    }

    // ---- Vista previa ---------------------------------------------------------------------------------------

    /// <summary>The checkbox of a row (PLA-015).</summary>
    /// <param name="index">The row.</param>
    public void ToggleRow(int index)
    {
        _t.Preview.Toggle(index);
        Invalidate();
    }

    /// <summary>✏ of a row: opens or closes its name field (PLA-015).</summary>
    /// <param name="index">The row.</param>
    public void EditRow(int index)
    {
        _editingRow = _editingRow == index ? null : index;
        Invalidate();
    }

    /// <summary>The name field of a row: an empty name goes back to the original (PLA-015).</summary>
    /// <param name="index">The row.</param>
    /// <param name="text">What the person typed or dictated.</param>
    public void RenameRow(int index, string text)
    {
        _t.Preview.Rename(index, text);
        Invalidate();
    }

    /// <summary>The final button of the preview (PLA-017).</summary>
    public void InstallPreview()
    {
        if (_t.Preview.Action == PreviewAction.EditShortcuts && _t.Preview.Installed() is { } done)
        {
            _openProfile(done.Id, false);
            return;
        }

        var result = _t.Preview.Install(Language);
        if (result.TryGetValue(out var outcome))
        {
            _editingRow = null;
            if (_t.Preview.Source is null)
            {
                _selectedTemplate = null;
            }

            Announce(outcome);
        }

        Invalidate();
    }

    private string T(Message message) => _s.Localization.Current.Format(message);

    private async Task DictateAsync() =>
        _ = await _s.Dictate(CancellationToken.None).ConfigureAwait(true);

    private static string Count(int count) => count.ToString(CultureInfo.InvariantCulture);

    private void Notify(Message text, string icon, bool warning, bool undo = false) =>
        _t.Notify(new WorkspaceNotice(text, icon, undo, warning));

    private void Announce(InstallOutcome outcome)
    {
        if (outcome.Notice is { } notice)
        {
            Notify(notice, "download_done", false, undo: true);
        }
    }

    private void Open(ProfileId id, bool capture, ProcessName? process)
    {
        _openProfile(id, true);
        if (capture)
        {
            _s.Profiles.StartCapture();
        }
        else if (
            process is { } p
            && _s.Store.Current.Library.ProfileFor(p) is { } owner
            && owner.Id != id
        )
        {
            _s.Profiles.Bind(p);
        }
    }

    private async Task RunAsync(string app)
    {
        _keyOpen = false;
        var catalogs = _s.Catalogs();
        var icons = catalogs
            .Icons.Entries.Items.Select(e => e.Icon.Name)
            .ToImmutableHashSet(StringComparer.Ordinal);
        var task = _t.Ai.GenerateAsync(
            app,
            Layout(),
            Language,
            chord => ComboWarnings.Of(chord) == ComboWarning.Blocked,
            icon => icons.IsEmpty || icons.Contains(icon),
            CancellationToken.None
        );
        Refresh();
        var template = await task.ConfigureAwait(true);
        if (template is not null)
        {
            _t.Preview.ShowAi(template);
            _selectedTemplate = null;
            _editingRow = null;
        }

        _hasKey = _t.Ai.HasKey;
        Refresh();
    }

    private async Task LoadAppsAsync()
    {
        var apps = await _s.OpenApps(CancellationToken.None).ConfigureAwait(true);
        _s.Post(() =>
        {
            _apps = apps;
            Invalidate();
        });
    }

    private string Layout() =>
        KeyboardLayouts.Effective(Settings.Keyboard.Layout, _t.DetectedLayout());

    private string BlankIcon()
    {
        if (_blankIcon is { } chosen)
        {
            return chosen;
        }

        var suggested = IconSuggestions.Suggest(
            _blankName,
            null,
            Settings.Keyboard.AppsLanguage,
            _s.Catalogs().Icons,
            _s.Catalogs().Combos
        );
        return suggested.IsEmpty ? IconCatalog.ProfileDefault.Name : suggested[0].Name;
    }

    private TemplatesScreen Build()
    {
        var library = _s.Store.Current.Library;
        var (suggested, available) = Cards(library);
        return new TemplatesScreen(
            T(L.TplTitle),
            T(L.TplSub2),
            AiCard(),
            Blank(),
            new SuggestedModel(
                T(L.TplSuggested),
                T(L.AsShort),
                Settings.AutoSuggestProfiles,
                Settings.AutoSuggestProfiles ? suggested : [],
                Settings.AutoSuggestProfiles && !suggested.IsEmpty
                    ? null
                    : T(Settings.AutoSuggestProfiles ? L.NoSugOn : L.NoSugOff),
                T(L.Preview),
                T(L.InstallBtn)
            ),
            T(L.TplAvail),
            available,
            available.IsEmpty ? T(L.AllInstalled) : null,
            T(L.InstallBtn),
            new InstalledModel(
                T(L.TplInst),
                Count(library.Profiles.Count),
                [
                    .. library.Profiles.Select(p => new InstalledProfile(
                        p.Id,
                        p.Icon.Name,
                        p.Name.Get(Language, LangCode.Es),
                        Count(p.Shortcuts.Count),
                        T(L.ShortcutsN(p.Shortcuts.Count))
                    )),
                ],
                T(L.ImportProf),
                T(L.ShareHint)
            ),
            Preview()
        );
    }

    private (ValueList<TemplateCard> Suggested, ValueList<TemplateCard> Available) Cards(
        ShortcutLibrary library
    )
    {
        if (Content is not { } content)
        {
            return ([], []);
        }

        var suggested = new List<TemplateCard>();
        var shown = new HashSet<string>(StringComparer.Ordinal);
        foreach (var app in _apps)
        {
            if (
                content.TemplateFor(app.Process) is { } template
                && library.ProfileFor(app.Process) is null
                && TemplatePreviewRules.InstalledFrom(library, template.Id) is null
                && shown.Add(template.Id)
            )
            {
                suggested.Add(
                    Card(
                        template,
                        T(
                            L.SugLine(
                                app: template.Name.Get(Language, LangCode.Es),
                                count: template.Shortcuts.Count
                            )
                        )
                    )
                );
            }
        }

        var detect = Settings.AutoSuggestProfiles;
        var available = content
            .Templates.Items.Where(t =>
                TemplatePreviewRules.InstalledFrom(library, t.Id) is null
                && !(detect && shown.Contains(t.Id))
            )
            .Select(t => Card(t, T(L.ShortcutsN(t.Shortcuts.Count))))
            .ToList();
        return ([.. suggested], [.. available]);
    }

    private TemplateCard Card(ProfileTemplate template, string meta) =>
        new(
            template.Id,
            template.Icon.Name,
            template.Name.Get(Language, LangCode.Es),
            meta,
            [.. template.Shortcuts.Items.Take(6).Select(s => s.Icon.Name)],
            string.Equals(_selectedTemplate, template.Id, StringComparison.Ordinal)
                && _t.Preview.Source == PreviewSource.Template
        );

    private AiCardModel AiCard()
    {
        var ai = _t.Ai;
        return new AiCardModel(
            T(L.AiHero),
            T(L.AiHeroD),
            T(L.AiPh),
            _query,
            T(L.DictName),
            T(ai.Generating ? L.Generating : L.Generate),
            !ai.Generating && _query.Trim().Length > 0,
            AiExamples.All,
            T(L.AiPrivacy4),
            T(_hasKey ? L.QuotaKey : L.KeyNone),
            T(_hasKey ? L.KeyChange : L.KeyUse),
            _keyOpen,
            T(L.KeyPh),
            T(L.KeyPaste),
            T(L.Done),
            _hasKey ? T(DeleteKeyArmed ? L.DelConfirm : L.KeyDelete) : null,
            ai.AskingConsent
                ? new ConsentModel(T(L.ConsentT), T(L.ConsentD4), T(L.ConsentOk), T(L.ConsentNo))
                : null,
            Error(ai.Error),
            Keyboard()
        );
    }

    private AiErrorModel? Error(AiError error)
    {
        var (icon, title, text, first) = error switch
        {
            AiError.Off => (
                "block",
                L.ErrAiOffT,
                L.ErrAiOffD,
                (ErrorActionKind.Enable, L.AiEnable)
            ),
            AiError.Offline => ("wifi_off", L.ErrOffT, L.ErrOffD, (ErrorActionKind.Retry, L.Retry)),
            AiError.NoKey => ("key", L.ErrNoKeyT, L.ErrNoKeyD, (ErrorActionKind.Key, L.KeyUse)),
            AiError.BadKey => ("key_off", L.ErrKeyT, L.ErrKeyD, (ErrorActionKind.Key, L.KeyChange)),
            AiError.Unavailable => (
                "cloud_off",
                L.ErrUnavT,
                L.ErrUnavD,
                (ErrorActionKind.Retry, L.Retry)
            ),
            AiError.Invalid => ("error", L.ErrInvT, L.ErrInvD, (ErrorActionKind.Retry, L.Retry)),
            _ => (string.Empty, L.Done, L.Done, (ErrorActionKind.Templates, L.Done)),
        };
        return error == AiError.None
            ? null
            : new AiErrorModel(
                icon,
                T(title),
                T(text),
                [
                    new ErrorAction(first.Item1, T(first.Item2)),
                    new ErrorAction(ErrorActionKind.Blank, T(L.BlankShort)),
                    new ErrorAction(ErrorActionKind.Templates, T(L.SeeTpl)),
                ]
            );
    }

    private KeyboardModel Keyboard()
    {
        var detected = _t.DetectedLayout();
        var layout = Layout();
        var apps = Settings.Keyboard.AppsLanguage;
        var detectedApps = _t.DetectedAppsLanguage();
        return new KeyboardModel(
            T(L.KbForLine(app: T(AppsLabel(apps)), name: T(LayoutLabel(layout)))),
            T(L.KbWhy),
            T(_keyboardOpen ? L.Done : L.Change),
            _keyboardOpen,
            T(L.KbLayout),
            [
                .. KeyboardLayouts.All.Items.Select(id => new KbOption(
                    id,
                    T(LayoutLabel(id)),
                    string.Equals(id, layout, StringComparison.Ordinal),
                    string.Equals(id, detected, StringComparison.Ordinal)
                )),
            ],
            T(L.KbApps),
            [
                .. KeyboardLayouts.AppsLanguages.Items.Select(code => new KbOption(
                    code.Value,
                    T(AppsLabel(code)),
                    code == apps,
                    code == detectedApps
                )),
            ],
            T(L.Detected)
        );
    }

    private static Message LayoutLabel(string id) =>
        id switch
        {
            KeyboardLayouts.SpanishSpain => L.KbEsEs,
            KeyboardLayouts.EnglishUs => L.KbEnUs,
            KeyboardLayouts.EnglishInternational => L.KbEnInt,
            _ => L.KbEsLa,
        };

    private static Message AppsLabel(LangCode code) =>
        code == LangCode.En ? L.KbAppsEn : L.KbAppsEs;

    private BlankModel Blank()
    {
        var icon = BlankIcon();
        var icons = new List<string> { icon };
        icons.AddRange(
            _s.Catalogs()
                .Icons.ProfileFeatured.Select(i => i.Name)
                .Where(i => !string.Equals(i, icon, StringComparison.Ordinal))
        );
        var links = _apps
            .Select(a => new BlankLink(a.Process.Value, "apps", a.Name, false))
            .Append(new BlankLink(Detect, "radar", T(L.LinkDetectShort), false))
            .Append(new BlankLink(NoLink, "link_off", T(L.LinkNoneShort), false))
            .Select(l =>
                l with
                {
                    Selected = string.Equals(l.Id, _blankLink, StringComparison.OrdinalIgnoreCase),
                }
            );
        return new BlankModel(
            T(L.BlankTitle),
            T(L.BlankSub),
            _blankOpen,
            icon,
            T(L.ChangeIcon),
            _blankIconsOpen,
            [
                .. icons.Select(i => new IconOption(
                    i,
                    string.Equals(i, icon, StringComparison.Ordinal)
                )),
            ],
            T(L.BlankPh),
            _blankName,
            T(L.DictName),
            T(L.BlankLink),
            [.. links],
            T(L.BlankCreate),
            _blankName.Trim().Length > 0
        );
    }

    private PreviewModel Preview()
    {
        var preview = _t.Preview;
        var keyboard = Keyboard().Line;
        if (preview.Draft() is not { } draft)
        {
            return new PreviewModel(
                false,
                T(L.PvEmpty),
                "preview",
                string.Empty,
                string.Empty,
                null,
                null,
                null,
                keyboard,
                null,
                null,
                [],
                string.Empty,
                string.Empty,
                false,
                false
            );
        }

        var rows = preview.Rows();
        var installed = preview.Installed() is not null;
        var missing = rows.Count(r => !r.AlreadyIn);
        var action = preview.Action;
        var count = preview.Count;
        var name = draft.Name.Get(Language, LangCode.Es);
        var labels = _s.Catalogs().KeyLabels;
        var (text, icon) = action switch
        {
            PreviewAction.AddMissing => (T(L.AddMissing(count)), "add"),
            PreviewAction.EditShortcuts => (T(L.EditShortcuts), "edit"),
            PreviewAction.CreateWith => (T(L.CreateWithN(count)), "auto_awesome"),
            _ => (T(L.InstallSelN(count)), "download"),
        };
        return new PreviewModel(
            true,
            T(L.PvEmpty),
            draft.Icon.Name,
            name,
            draft.Binding is AppBinding.Processes processes
                ? string.Join(", ", processes.Names.Items.Select(p => p.Value))
                : string.Empty,
            preview.IsUnknown ? T(L.UnknownMsg(app: name)) : null,
            preview.IsUnknown ? T(L.UnknownBlank(app: name)) : null,
            installed ? T(missing > 0 ? L.InstNoteSome(missing) : L.InstNoteAll) : null,
            keyboard,
            preview.OnlyOtherLanguage ? T(L.OnlyEs) : null,
            preview.UnavailableTexts > 0
                ? T(L.SharedTextsExcluded(preview.UnavailableTexts))
                : null,
            [
                .. rows.Select(r => new PreviewRowModel(
                    r.Index,
                    r.Shortcut.Icon.Name,
                    r.Shortcut.Name.Get(Language, LangCode.Es),
                    r.AlreadyIn ? T(L.AlreadyIn) : Foot(r.Shortcut, labels),
                    r.AlreadyIn,
                    r.Checked,
                    _editingRow == r.Index && !r.AlreadyIn,
                    r.Risky ? T(L.RiskyMark)
                        : r.Dangerous ? T(L.DangerMark)
                        : null,
                    T(L.Rename)
                )),
            ],
            text,
            icon,
            action == PreviewAction.EditShortcuts || count > 0,
            action == PreviewAction.EditShortcuts
        );
    }

    private string Foot(Shortcut shortcut, KeyLabelCatalog labels) =>
        ActionKinds.ChordOf(shortcut.Action) is { } chord
            ? KeyChordFormatter.Format(chord, labels, KeyLabelStyle.Full, Language, LangCode.Es)
        : shortcut.Action is MacroAction macro ? T(L.StepsN(macro.Steps.Count))
        : string.Empty;
}
