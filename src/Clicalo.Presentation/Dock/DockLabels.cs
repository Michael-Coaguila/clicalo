using Clicalo.Application.Localization;
using Clicalo.Domain.Messages;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clicalo.Presentation.Dock;

/// <summary>
/// The fixed texts of the handle, the bar and the windows beside it (docs/04 «Vista pestaña»), from <c>data/i18n</c> in
/// the interface language; formatted again when it changes (IDI-001).
/// </summary>
public sealed class DockLabels : ObservableObject
{
    private readonly ILocalizationContext _localization;
    private string _openBar = string.Empty;
    private string _hideBar = string.Empty;
    private string _expand = string.Empty;
    private string _frequents = string.Empty;
    private string _switchProfile = string.Empty;
    private string _search = string.Empty;
    private string _repeat = string.Empty;
    private string _pinnedShort = string.Empty;
    private string _pinned = string.Empty;
    private string _sticky = string.Empty;
    private string _previousPage = string.Empty;
    private string _nextPage = string.Empty;
    private string _pickProfile = string.Empty;
    private string _releaseAll = string.Empty;
    private string _coachSkip = string.Empty;
    private string _activeApp = string.Empty;

    /// <summary>Creates the texts in the current language.</summary>
    /// <param name="localization">The interface language.</param>
    public DockLabels(ILocalizationContext localization)
    {
        ArgumentNullException.ThrowIfNull(localization);
        _localization = localization;
        Relocalize();
    }

    /// <summary>[openBar]: the handle.</summary>
    public string OpenBar
    {
        get => _openBar;
        private set => SetProperty(ref _openBar, value);
    }

    /// <summary>[hideBar]: the chevron of the bar.</summary>
    public string HideBar
    {
        get => _hideBar;
        private set => SetProperty(ref _hideBar, value);
    }

    /// <summary>[expand]: open_in_full.</summary>
    public string Expand
    {
        get => _expand;
        private set => SetProperty(ref _expand, value);
    }

    /// <summary>[freq]: ★.</summary>
    public string Frequents
    {
        get => _frequents;
        private set => SetProperty(ref _frequents, value);
    }

    /// <summary>[switchProf]: the profile button.</summary>
    public string SwitchProfile
    {
        get => _switchProfile;
        private set => SetProperty(ref _switchProfile, value);
    }

    /// <summary>[searchA]: 🔍.</summary>
    public string Search
    {
        get => _search;
        private set => SetProperty(ref _search, value);
    }

    /// <summary>[repeat]: ↻.</summary>
    public string Repeat
    {
        get => _repeat;
        private set => SetProperty(ref _repeat, value);
    }

    /// <summary>[alwaysShort]: «Fijos» on the 📌 button.</summary>
    public string PinnedShort
    {
        get => _pinnedShort;
        private set => SetProperty(ref _pinnedShort, value);
    }

    /// <summary>[always]: the name of the 📌 button and the header of its window.</summary>
    public string Pinned
    {
        get => _pinned;
        private set => SetProperty(ref _pinned, value);
    }

    /// <summary>[stickyMods]: the sticky keys button and its window.</summary>
    public string Sticky
    {
        get => _sticky;
        private set => SetProperty(ref _sticky, value);
    }

    /// <summary>[prevPage].</summary>
    public string PreviousPage
    {
        get => _previousPage;
        private set => SetProperty(ref _previousPage, value);
    }

    /// <summary>[nextPage].</summary>
    public string NextPage
    {
        get => _nextPage;
        private set => SetProperty(ref _nextPage, value);
    }

    /// <summary>[pickProfile]: the header of the profile grid beside the bar.</summary>
    public string PickProfile
    {
        get => _pickProfile;
        private set => SetProperty(ref _pickProfile, value);
    }

    /// <summary>[releaseAll]: the floating button.</summary>
    public string ReleaseAll
    {
        get => _releaseAll;
        private set => SetProperty(ref _releaseAll, value);
    }

    /// <summary>[coachSkip].</summary>
    public string CoachSkip
    {
        get => _coachSkip;
        private set => SetProperty(ref _coachSkip, value);
    }

    /// <summary>[activeApp]: the dot of the profile of the active app.</summary>
    public string ActiveApp
    {
        get => _activeApp;
        private set => SetProperty(ref _activeApp, value);
    }

    /// <summary>Formats every text again in the current language.</summary>
    public void Relocalize()
    {
        var l = _localization.Current;
        OpenBar = l.Format(L.OpenBar);
        HideBar = l.Format(L.HideBar);
        Expand = l.Format(L.Expand);
        Frequents = l.Format(L.Freq);
        SwitchProfile = l.Format(L.SwitchProf);
        Search = l.Format(L.SearchA);
        Repeat = l.Format(L.Repeat);
        PinnedShort = l.Format(L.AlwaysShort);
        Pinned = l.Format(L.Always);
        Sticky = l.Format(L.StickyMods);
        PreviousPage = l.Format(L.PrevPage);
        NextPage = l.Format(L.NextPage);
        PickProfile = l.Format(L.PickProfile);
        ReleaseAll = l.Format(L.ReleaseAll);
        CoachSkip = l.Format(L.CoachSkip);
        ActiveApp = l.Format(L.ActiveApp);
    }
}
