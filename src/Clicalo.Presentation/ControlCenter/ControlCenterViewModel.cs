using Clicalo.Application.UseCases.Editor;
using Clicalo.Domain.Commands;
using Clicalo.Domain.Guides;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.Presentation.ControlCenter.About;
using Clicalo.Presentation.ControlCenter.General;
using Clicalo.Presentation.ControlCenter.Shortcuts;
using Clicalo.Presentation.ControlCenter.SystemSection;
using Clicalo.Presentation.ControlCenter.Templates;
using Clicalo.Presentation.ControlCenter.TouchPrecision;
using Clicalo.Presentation.Guides;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clicalo.Presentation.ControlCenter;

/// <summary>
/// The frame of the Control Center (CCM-001 to CCM-003): the title bar with the path and the ES/EN selector, the side
/// menu with its six sections and the status bar with [undo]. A section whose services are not composed (tests) shows a
/// marker.
/// </summary>
public sealed class ControlCenterViewModel : ObservableObject
{
    private readonly ControlCenterServices _s;
    private readonly Action _close;
    private ControlCenterSection _section = ControlCenterSection.Shortcuts;
    private WorkspaceNotice? _notice;
    private ValueList<NavItem> _nav = [];
    private ValueList<LanguageOption> _languages = [];
    private StatusModel _status = new(
        string.Empty,
        string.Empty,
        false,
        false,
        string.Empty,
        string.Empty
    );
    private string _sectionTitle = string.Empty;
    private string _title = string.Empty;
    private string _closeName = string.Empty;
    private string _closeHelp = string.Empty;
    private string _minimizeName = string.Empty;
    private string _maximizeName = string.Empty;
    private string _restoreName = string.Empty;
    private string _soon = string.Empty;
    private string _languageName = string.Empty;
    private string _appName = string.Empty;

    /// <summary>Creates the frame.</summary>
    /// <param name="services">The services of the Control Center.</param>
    /// <param name="close">Closes the window (✕, Esc).</param>
    public ControlCenterViewModel(ControlCenterServices services, Action close)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(close);
        _s = services;
        _close = close;
        Guide = new GuideViewModel(services.Localization, GuideDone);
        Shortcuts = new ShortcutsSectionViewModel(services);
        Shortcuts.ListChosen += (_, _) => Guide.Signal(GuideSignal.ProfileChosen);
        Shortcuts.PropertyChanged += (_, e) =>
        {
            if (
                string.Equals(
                    e.PropertyName,
                    nameof(ShortcutsSectionViewModel.RepeatedCount),
                    StringComparison.Ordinal
                )
            )
            {
                Refresh();
            }
        };
        if (services.System is { } system)
        {
            System = new SystemSectionViewModel(services, system);
            System.TabChosen += (_, _) => Guide.Signal(GuideSignal.SystemTabChanged);
            System.PropertyChanged += (_, e) =>
            {
                if (
                    string.Equals(
                        e.PropertyName,
                        nameof(SystemSectionViewModel.UpdateCount),
                        StringComparison.Ordinal
                    )
                )
                {
                    Refresh();
                }
            };
        }

        Templates = services.Templates is { } templates
            ? new TemplatesSectionViewModel(services, templates, OpenProfile)
            : null;
        General = new GeneralSectionViewModel(
            new GeneralServices(
                services.Store,
                services.Localization,
                services.Confirm,
                services.Time,
                services.Post,
                services.OpenWelcome ?? (static () => { }),
                services.Templates?.Ai,
                () => services.Catalogs().KeyLabels,
                Templates is null ? null : OpenKey
            )
        );
        TouchPrecision = new TouchPrecisionViewModel(
            new TouchPrecisionServices(
                services.Store,
                services.Localization,
                services.Time,
                services.Post
            )
        );
        TouchPrecision.Tested += (_, _) => Guide.Signal(GuideSignal.TestTapped);
        if (services.About is { } about)
        {
            var aboutSection = new AboutViewModel(about);
            About = aboutSection;
            services.Localization.LanguageChanged += (_, _) => services.Post(aboutSection.Refresh);
        }

        Refresh();
    }

    /// <summary>The «General y panel» section (docs/05 §3).</summary>
    public GeneralSectionViewModel General { get; }

    /// <summary>The guide of the section in view (R-39), until it is finished or skipped.</summary>
    public GuideViewModel Guide { get; }

    /// <summary>The «Precisión táctil» section (docs/05 §4).</summary>
    public TouchPrecisionViewModel TouchPrecision { get; }

    /// <summary>The «Acerca de y contacto» section (docs/05 §6); null while its services are not composed.</summary>
    public AboutViewModel? About { get; }

    /// <summary>The «Plantillas» section; null without its services.</summary>
    public TemplatesSectionViewModel? Templates { get; }

    /// <summary>The «Atajos» section.</summary>
    public ShortcutsSectionViewModel Shortcuts { get; }

    /// <summary>The «Sistema» section (docs/05 §5); null while its services are not composed.</summary>
    public SystemSectionViewModel? System { get; }

    /// <summary>The section in view.</summary>
    public ControlCenterSection Section
    {
        get => _section;
        private set => SetProperty(ref _section, value);
    }

    /// <summary>The side menu (CCM-002).</summary>
    public ValueList<NavItem> Nav
    {
        get => _nav;
        private set => SetProperty(ref _nav, value);
    }

    /// <summary>The ES/EN selector (CCM-001).</summary>
    public ValueList<LanguageOption> Languages
    {
        get => _languages;
        private set => SetProperty(ref _languages, value);
    }

    /// <summary>[lang], the accessible name of the selector.</summary>
    public string LanguageName
    {
        get => _languageName;
        private set => SetProperty(ref _languageName, value);
    }

    /// <summary>[appName], next to the logo.</summary>
    public string AppName
    {
        get => _appName;
        private set => SetProperty(ref _appName, value);
    }

    /// <summary>[cc], the second step of the path and the window title.</summary>
    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    /// <summary>The name of the section in view, the third step of the path.</summary>
    public string SectionTitle
    {
        get => _sectionTitle;
        private set => SetProperty(ref _sectionTitle, value);
    }

    /// <summary>[close].</summary>
    public string CloseName
    {
        get => _closeName;
        private set => SetProperty(ref _closeName, value);
    }

    /// <summary>[closeEsc], the tooltip of ✕.</summary>
    public string CloseHelp
    {
        get => _closeHelp;
        private set => SetProperty(ref _closeHelp, value);
    }

    /// <summary>[min], the name of the minimize button of the title bar (D13).</summary>
    public string MinimizeName
    {
        get => _minimizeName;
        private set => SetProperty(ref _minimizeName, value);
    }

    /// <summary>[winMaximize], the name of the maximize button while the window is not maximized (D13).</summary>
    public string MaximizeName
    {
        get => _maximizeName;
        private set => SetProperty(ref _maximizeName, value);
    }

    /// <summary>[winRestore], the name of the same button while the window is maximized (D13).</summary>
    public string RestoreName
    {
        get => _restoreName;
        private set => SetProperty(ref _restoreName, value);
    }

    /// <summary>The marker of the sections that arrive in the next part.</summary>
    public string Soon
    {
        get => _soon;
        private set => SetProperty(ref _soon, value);
    }

    /// <summary>The status bar (CCM-003).</summary>
    public StatusModel Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    /// <summary>An item of the side menu.</summary>
    /// <param name="section">The section.</param>
    public void Select(ControlCenterSection section)
    {
        Section = section;
        if (section != ControlCenterSection.Shortcuts)
        {
            // EDI-010: «Grabar con teclado» belongs to the editor; in another section the keyboard is free again.
            Shortcuts.Editor.StopRecording();
        }

        if (section == ControlCenterSection.System)
        {
            System?.OnShown();
        }

        if (section == ControlCenterSection.Panel)
        {
            General.OnShown();
        }

        if (section == ControlCenterSection.Templates)
        {
            // The open apps and the key are read again each time the section shows (PLA-011, PLA-003).
            Templates?.OnOpened();
        }

        Refresh();
    }

    /// <summary>A button of the ES/EN selector (CCM-001, IDI-001): the language changes in place in every window.</summary>
    /// <param name="code">The language code.</param>
    public void SetLanguage(string code)
    {
        if (!string.Equals(code, _s.Localization.Current.Locale.Code, StringComparison.Ordinal))
        {
            _ = _s.Store.Dispatch(new SetSetting(SettingPaths.Language, new LangCode(code)));
        }
    }

    /// <summary>[undo] of the status bar (CCM-003): [restoredU].</summary>
    public void Undo()
    {
        if (_s.Store.Undo().IsSuccess)
        {
            ShowNotice(new WorkspaceNotice(L.RestoredU, "undo", false, false));
        }
    }

    /// <summary>
    /// Esc outside a field (CCM-001): closes the open menu, or the window when there is none. The view leaves a field
    /// first.
    /// </summary>
    public void Escape()
    {
        if (Section == ControlCenterSection.Shortcuts && Shortcuts.CloseMenu())
        {
            return;
        }

        if (Section == ControlCenterSection.System && (System?.CloseMenu() ?? false))
        {
            return;
        }

        if (Section == ControlCenterSection.Templates && Templates?.CloseMenu() == true)
        {
            return;
        }

        _close();
    }

    /// <summary>✕ of the title bar.</summary>
    public void Close() => _close();

    /// <summary>Shows a message in the status bar; the composition root clears it after its time (AVI-002).</summary>
    /// <param name="notice">The message.</param>
    public void ShowNotice(WorkspaceNotice notice)
    {
        _notice = notice;
        Refresh();
    }

    /// <summary>The message is over: [saved] again.</summary>
    public void ClearNotice()
    {
        _notice = null;
        Refresh();
    }

    /// <summary>Projects the frame again (language, counts, undo).</summary>
    public void Refresh()
    {
        Templates?.Invalidate();
        var localizer = _s.Localization.Current;
        AppName = T(L.AppName);
        Title = T(L.Cc);
        CloseName = T(L.Close);
        CloseHelp = T(L.CloseEsc);
        MinimizeName = T(L.Min);
        MaximizeName = T(L.WinMaximize);
        RestoreName = T(L.WinRestore);
        Soon = T(L.CcSoon);
        LanguageName = T(L.Lang);
        var repeated = Shortcuts.RepeatedCount;
        Nav =
        [
            Item(ControlCenterSection.Shortcuts, "tune", L.NavShort, repeated, false),
            Item(ControlCenterSection.Templates, "auto_awesome", L.NavTpl, 0, false),
            Item(ControlCenterSection.Panel, "display_settings", L.NavPanel, 0, false),
            Item(ControlCenterSection.Touch, "touch_app", L.NavTouch, 0, false),
            Item(
                ControlCenterSection.System,
                "verified_user",
                L.NavSys,
                System?.UpdateCount ?? 0,
                true,
                L.UpdAvail
            ),
            Item(ControlCenterSection.About, "favorite", L.NavAbout2, 0, false),
        ];
        SectionTitle = Nav.Items.First(n => n.Section == Section).Label;
        Guide.Show(GuideRules.InControlCenter(GuideOf(Section), _s.Store.Current.Settings));
        Languages =
        [
            .. _s.Localization.Languages.Select(locale => new LanguageOption(
                locale.Code,
                locale.ShortName,
                locale.NativeName,
                string.Equals(locale.Code, localizer.Locale.Code, StringComparison.Ordinal)
            )),
        ];
        var canUndo = _s.Store.CanUndo && (_notice?.CanUndo ?? false);
        Status = _notice is { } notice
            ? new StatusModel(
                notice.Icon,
                T(notice.Text),
                notice.IsWarning,
                canUndo,
                T(L.Undo),
                T(notice.UndoName ?? L.Undo),
                IsNotice: true
            )
            : new StatusModel("info", T(L.Saved), false, false, T(L.Undo), T(L.Undo));
    }

    private string T(Message message) => _s.Localization.Current.Format(message);

    /// <summary>The guide of <paramref name="section"/> (R-39).</summary>
    private static GuideId GuideOf(ControlCenterSection section) =>
        section switch
        {
            ControlCenterSection.Templates => GuideId.Templates,
            ControlCenterSection.Panel => GuideId.General,
            ControlCenterSection.Touch => GuideId.Touch,
            ControlCenterSection.System => GuideId.System,
            ControlCenterSection.About => GuideId.About,
            _ => GuideId.Shortcuts,
        };

    /// <summary>R-39: the guide of a section was finished or skipped; it does not show again until General asks for it.</summary>
    private void GuideDone(GuideId guide) =>
        _ = _s.Store.Dispatch(
            new SetSetting(
                SettingPaths.GuidesDone,
                GuideProgress.WithDone(_s.Store.Current.Settings.GuidesDone, guide)
            )
        );

    // A profile created or chosen in Plantillas: «Atajos» on it, with the library when it is new (PLA-010, PLA-014).
    private void OpenProfile(ProfileId profile, bool library)
    {
        _s.Shortcuts.Open(new ListRef.InProfile(profile), null, library);
        Shortcuts.OnOpened();
        Select(ControlCenterSection.Shortcuts);
    }

    // «Usar mi clave» of General › IA: the key is pasted in Plantillas, next to «Crear con IA» (GEN-015, PLA-003).
    private void OpenKey()
    {
        Templates?.OpenKey();
        Select(ControlCenterSection.Templates);
    }

    private NavItem Item(
        ControlCenterSection section,
        string icon,
        Message label,
        int count,
        bool separator,
        Message? countName = null
    ) =>
        new(
            section,
            icon,
            T(label),
            count,
            count > 0
                ? countName is { } name
                    ? T(name)
                    : T(L.DupSummaryN(count))
                : string.Empty,
            section == Section,
            separator
        );
}
