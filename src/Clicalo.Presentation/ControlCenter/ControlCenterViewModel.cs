using System.Globalization;
using Clicalo.Application.UseCases.Editor;
using Clicalo.Domain.Commands;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.Presentation.ControlCenter.Shortcuts;
using Clicalo.Presentation.ControlCenter.SystemSection;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clicalo.Presentation.ControlCenter;

/// <summary>
/// The frame of the Control Center (CCM-001 to CCM-003): the title bar with the path and the ES/EN selector, the side
/// menu with its six sections and the status bar with [undo]. Sections 2 to 6 show a marker until their part arrives.
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
        Shortcuts = new ShortcutsSectionViewModel(services);
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

        Refresh();
    }

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
        if (section == ControlCenterSection.System)
        {
            System?.OnShown();
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
        var localizer = _s.Localization.Current;
        AppName = T(L.AppName);
        Title = T(L.Cc);
        CloseName = T(L.Close);
        CloseHelp = T(L.CloseEsc);
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
                T(notice.UndoName ?? L.Undo)
            )
            : new StatusModel("info", T(L.Saved), false, false, T(L.Undo), T(L.Undo));
    }

    private string T(Message message) => _s.Localization.Current.Format(message);

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
                    : count.ToString(CultureInfo.InvariantCulture) + " " + T(L.DupSummary)
                : string.Empty,
            section == Section,
            separator
        );
}
