using Clicalo.Domain.PanelLayout;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clicalo.Presentation.Panel;

/// <summary>
/// The profile selector of the Full view (SEL-001, SEL-002): two columns of the same width, ★ Frequents and the profile
/// button with its icon, name, the dot of the active app and ▾, ▴ or ↶. The button is an ExpandCollapse for UI
/// Automation. What a tap does comes from <see cref="SelectorRules"/>.
/// </summary>
public sealed class SelectorRowViewModel : ObservableObject
{
    private readonly IPanelBodyIntents _intents;
    private bool _isVisible;
    private bool _frequents;
    private string _frequentsName = string.Empty;
    private string _frequentsState = string.Empty;
    private string _profileName = string.Empty;
    private string _profileIcon = string.Empty;
    private bool _isActiveApp;
    private string _activeAppName = string.Empty;
    private string _buttonHelp = string.Empty;
    private SelectorCaret _caret;
    private SelectorLook _look;

    internal SelectorRowViewModel(IPanelBodyIntents intents) => _intents = intents;

    /// <summary>Whether the row shows (SEL-001).</summary>
    public bool IsVisible
    {
        get => _isVisible;
        private set => SetProperty(ref _isVisible, value);
    }

    /// <summary>Whether Frequents is in view: ★ is filled with accent.</summary>
    public bool IsFrequentsActive
    {
        get => _frequents;
        private set => SetProperty(ref _frequents, value);
    }

    /// <summary>[freq].</summary>
    public string FrequentsName
    {
        get => _frequentsName;
        private set => SetProperty(ref _frequentsName, value);
    }

    /// <summary>The state of ★ in words for UI Automation while Frequents is in view ([on]); never color alone.</summary>
    public string FrequentsState
    {
        get => _frequentsState;
        private set => SetProperty(ref _frequentsState, value);
    }

    /// <summary>The profile the button shows: the one in view, or the return profile from Frequents.</summary>
    public string ProfileName
    {
        get => _profileName;
        private set => SetProperty(ref _profileName, value);
    }

    /// <summary>Its Material Symbols icon.</summary>
    public string ProfileIcon
    {
        get => _profileIcon;
        private set => SetProperty(ref _profileIcon, value);
    }

    /// <summary>Whether it is the profile of the active app: an 8 px dot (SEL-001).</summary>
    public bool IsActiveApp
    {
        get => _isActiveApp;
        private set => SetProperty(ref _isActiveApp, value);
    }

    /// <summary>[activeApp], the accessible name of the dot.</summary>
    public string ActiveAppName
    {
        get => _activeAppName;
        private set => SetProperty(ref _activeAppName, value);
    }

    /// <summary>[switchProf], help text of the button.</summary>
    public string ButtonHelp
    {
        get => _buttonHelp;
        private set => SetProperty(ref _buttonHelp, value);
    }

    /// <summary>▾, ▴ or ↶.</summary>
    public SelectorCaret Caret
    {
        get => _caret;
        private set
        {
            if (SetProperty(ref _caret, value))
            {
                OnPropertyChanged(nameof(IsExpanded));
            }
        }
    }

    /// <summary>Accent, cardHi or card with a border.</summary>
    public SelectorLook Look
    {
        get => _look;
        private set => SetProperty(ref _look, value);
    }

    /// <summary>The ExpandCollapse state: expanded while the profile grid is open.</summary>
    public bool IsExpanded => Caret == SelectorCaret.Collapse;

    /// <summary>★ Frequents (a tap or UI Automation Invoke).</summary>
    public void Frequents() => _intents.ShowFrequents();

    /// <summary>The profile button (a tap, or UI Automation Expand or Collapse), SEL-002.</summary>
    public void ProfileButton()
    {
        if (SelectorRules.Tap(IsFrequentsActive) == SelectorTap.ReturnFromFrequents)
        {
            _intents.ReturnFromFrequents();
        }
        else
        {
            _intents.TogglePicker();
        }
    }

    internal void Apply(
        bool visible,
        bool frequents,
        bool pickerOpen,
        bool activeApp,
        string profileName,
        string profileIcon,
        string frequentsName,
        string activeAppName,
        string switchProfile,
        string onWord
    )
    {
        IsFrequentsActive = frequents;
        FrequentsState = frequents ? onWord : string.Empty;
        FrequentsName = frequentsName;
        ProfileName = profileName;
        ProfileIcon = profileIcon;
        IsActiveApp = activeApp;
        ActiveAppName = activeAppName;
        ButtonHelp = switchProfile;
        Caret = SelectorRules.Caret(frequents, pickerOpen);
        Look = SelectorRules.Look(frequents, pickerOpen);
        IsVisible = visible;
    }
}
