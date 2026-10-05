using Clicalo.Application.Localization;
using Clicalo.Application.Profiles;
using Clicalo.Domain.Messages;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clicalo.Presentation.Panel.Header;

/// <summary>
/// The header of the full and compact views (CAB-001 to CAB-003, docs/04 §1): the grip ⋮⋮ and the title (both drag
/// the panel, PAN-004), the Auto/Fixed button and the Search, Edit, Quick settings and Minimize buttons. It applies
/// <see cref="PanelHeaderModel"/> and the open layers and formats every text from <c>data/i18n</c> when applied; it
/// decides no rule: Auto/Fixed goes to the <see cref="ProfileViewCoordinator"/> and the other buttons to
/// <see cref="PanelHeaderActions"/>.
/// </summary>
public sealed class PanelHeaderViewModel : ObservableObject
{
    private const string EditSymbol = "edit";
    private const string DoneSymbol = "check";

    private readonly ProfileViewCoordinator _profiles;
    private readonly ILocalizationContext _localization;
    private readonly PanelHeaderActions _actions;
    private PanelHeaderModel? _model;
    private string _title = string.Empty;
    private string _titleIcon = string.Empty;
    private bool _showsActiveAppDot;
    private string _activeAppDotName = string.Empty;
    private bool _isFixed;
    private bool _showsAutoFixed = true;
    private string _autoFixedIcon = string.Empty;
    private string _autoFixedName = string.Empty;
    private string _moveName = string.Empty;
    private string _searchName = string.Empty;
    private string _editName = string.Empty;
    private string _quickSettingsName = string.Empty;
    private string _minimizeName = string.Empty;
    private bool _isSearchOpen;
    private bool _isEditing;
    private bool _isQuickSettingsOpen;

    /// <summary>Creates the header.</summary>
    /// <param name="profiles">Where Auto/Fixed goes (PER-006).</param>
    /// <param name="localization">The interface language.</param>
    /// <param name="actions">What the other buttons do; a missing action hides its button.</param>
    public PanelHeaderViewModel(
        ProfileViewCoordinator profiles,
        ILocalizationContext localization,
        PanelHeaderActions actions
    )
    {
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(actions);
        _profiles = profiles;
        _localization = localization;
        _actions = actions;
        Relocalize();
    }

    /// <summary>The title: the profile name, «Frecuentes» or «Buscar» (CAB-002).</summary>
    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    /// <summary>The Material Symbols icon before the title.</summary>
    public string TitleIcon
    {
        get => _titleIcon;
        private set => SetProperty(ref _titleIcon, value);
    }

    /// <summary>Whether the «auto» dot shows: the profile shown is that of the active app (CAB-002).</summary>
    public bool ShowsActiveAppDot
    {
        get => _showsActiveAppDot;
        private set => SetProperty(ref _showsActiveAppDot, value);
    }

    /// <summary>The accessible name of the «auto» dot («app activa»).</summary>
    public string ActiveAppDotName
    {
        get => _activeAppDotName;
        private set => SetProperty(ref _activeAppDotName, value);
    }

    /// <summary>Fixed (red, lock) or Auto (blue, autorenew); the Toggle state of the button (CAB-003).</summary>
    public bool IsFixed
    {
        get => _isFixed;
        private set => SetProperty(ref _isFixed, value);
    }

    /// <summary>Whether the Auto/Fixed button shows: hidden while the search has text (CAB-001).</summary>
    public bool ShowsAutoFixed
    {
        get => _showsAutoFixed;
        private set => SetProperty(ref _showsAutoFixed, value);
    }

    /// <summary>The icon of the Auto/Fixed button.</summary>
    public string AutoFixedIcon
    {
        get => _autoFixedIcon;
        private set => SetProperty(ref _autoFixedIcon, value);
    }

    /// <summary>The accessible name and tooltip of the Auto/Fixed button ([autoA] or [lockA]).</summary>
    public string AutoFixedName
    {
        get => _autoFixedName;
        private set => SetProperty(ref _autoFixedName, value);
    }

    /// <summary>The accessible name of the grip ([move] «Mover panel»).</summary>
    public string MoveName
    {
        get => _moveName;
        private set => SetProperty(ref _moveName, value);
    }

    /// <summary>The accessible name of Search ([searchA]).</summary>
    public string SearchName
    {
        get => _searchName;
        private set => SetProperty(ref _searchName, value);
    }

    /// <summary>The accessible name of Edit ([edit]).</summary>
    public string EditName
    {
        get => _editName;
        private set => SetProperty(ref _editName, value);
    }

    /// <summary>The accessible name of Quick settings ([quick]).</summary>
    public string QuickSettingsName
    {
        get => _quickSettingsName;
        private set => SetProperty(ref _quickSettingsName, value);
    }

    /// <summary>The accessible name of Minimize ([min]).</summary>
    public string MinimizeName
    {
        get => _minimizeName;
        private set => SetProperty(ref _minimizeName, value);
    }

    /// <summary>Whether the search is open (the button gets the <c>cardHi</c> fill).</summary>
    public bool IsSearchOpen
    {
        get => _isSearchOpen;
        private set => SetProperty(ref _isSearchOpen, value);
    }

    /// <summary>Whether edit mode is on (the button becomes ✓ on <c>accent</c>, CAB-001).</summary>
    public bool IsEditing
    {
        get => _isEditing;
        private set
        {
            if (SetProperty(ref _isEditing, value))
            {
                OnPropertyChanged(nameof(EditIcon));
            }
        }
    }

    /// <summary>The icon of Edit: <c>edit</c>, or <c>check</c> while editing.</summary>
    public string EditIcon => _isEditing ? DoneSymbol : EditSymbol;

    /// <summary>Whether Quick settings are open (the button gets the <c>cardHi</c> fill).</summary>
    public bool IsQuickSettingsOpen
    {
        get => _isQuickSettingsOpen;
        private set => SetProperty(ref _isQuickSettingsOpen, value);
    }

    /// <summary>Whether Search has an action and shows.</summary>
    public bool HasSearch => _actions.Search is not null;

    /// <summary>Whether Edit has an action and shows.</summary>
    public bool HasEdit => _actions.Edit is not null;

    /// <summary>Whether Quick settings has an action and shows.</summary>
    public bool HasQuickSettings => _actions.QuickSettings is not null;

    /// <summary>Whether Minimize has an action and shows.</summary>
    public bool HasMinimize => _actions.Minimize is not null;

    /// <summary>Applies the projection of the header.</summary>
    /// <param name="model">The header model.</param>
    public void Apply(PanelHeaderModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        _model = model;
        TitleIcon = model.Icon.Name;
        ShowsActiveAppDot = model.ShowsActiveAppDot;
        IsFixed = model.IsFixed;
        ShowsAutoFixed = model.ShowsAutoFixed;
        AutoFixedIcon = model.AutoFixedIcon.Name;
        FormatModel();
    }

    /// <summary>Applies which layers are open: the search, edit mode and Quick settings.</summary>
    /// <param name="searchOpen">Whether the search is open.</param>
    /// <param name="editing">Whether edit mode is on.</param>
    /// <param name="quickSettingsOpen">Whether Quick settings are open.</param>
    public void ApplyLayers(bool searchOpen, bool editing, bool quickSettingsOpen)
    {
        IsSearchOpen = searchOpen;
        IsEditing = editing;
        IsQuickSettingsOpen = quickSettingsOpen;
    }

    /// <summary>Formats every text again in the current language (IDI-001).</summary>
    public void Relocalize()
    {
        var localizer = _localization.Current;
        MoveName = localizer.Format(L.Move);
        SearchName = localizer.Format(L.SearchA);
        EditName = localizer.Format(L.Edit);
        QuickSettingsName = localizer.Format(L.Quick);
        MinimizeName = localizer.Format(L.Min);
        ActiveAppDotName = localizer.Format(L.ActiveApp);
        FormatModel();
    }

    /// <summary>The Auto/Fixed button (a tap, or UI Automation Toggle): PER-006.</summary>
    public void ToggleLock() => _profiles.ToggleLock();

    /// <summary>The Search button.</summary>
    public void Search() => _actions.Search?.Invoke();

    /// <summary>The Edit button.</summary>
    public void Edit() => _actions.Edit?.Invoke();

    /// <summary>The Quick settings button.</summary>
    public void QuickSettings() => _actions.QuickSettings?.Invoke();

    /// <summary>The Minimize button.</summary>
    public void Minimize() => _actions.Minimize?.Invoke();

    private void FormatModel()
    {
        if (_model is null)
        {
            return;
        }

        var localizer = _localization.Current;
        Title = _model.Title is { } title ? localizer.Format(title) : _model.ProfileName;
        AutoFixedName = localizer.Format(_model.AutoFixedName);
    }
}
