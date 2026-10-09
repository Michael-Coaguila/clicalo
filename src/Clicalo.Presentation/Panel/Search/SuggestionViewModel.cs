using System.Collections.Immutable;
using Clicalo.Application.Localization;
using Clicalo.Application.Store;
using Clicalo.Application.UseCases;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.ProfileResolution;
using Clicalo.Domain.Templates;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clicalo.Presentation.Panel.Search;

/// <summary>
/// The suggestion card of the panel (PER-009, docs/04 §6): «<b>Excel</b> no tiene perfil. ¿Creo uno con atajos
/// listos?» with [Crear perfil] and [Ahora no]. When it shows and what «Crear perfil» does are
/// <see cref="ProfileSuggestions"/>; this view model only keeps the apps dismissed in this session and the texts.
/// </summary>
public sealed class SuggestionViewModel : ObservableObject
{
    private readonly DocumentStore _store;
    private readonly StarterContent? _content;
    private readonly ILocalizationContext _localization;
    private readonly IIdGenerator _ids;
    private readonly Func<ProfileState> _profileState;
    private readonly Action<SuggestionAccepted> _accepted;
    private readonly Action<Message> _notify;
    private ImmutableHashSet<ProcessName> _dismissed = [];
    private ProcessName _app;
    private bool _searching;
    private ProfileSuggestion? _suggestion;
    private string _appName = string.Empty;
    private string _message = string.Empty;
    private string _createText = string.Empty;
    private string _notNowText = string.Empty;

    /// <summary>Creates the card.</summary>
    /// <param name="store">The document: its profiles, the «Detectar» setting, and where «Crear perfil» installs.</param>
    /// <param name="content">The templates that ship with Clícalo; null when they could not be read (no card).</param>
    /// <param name="localization">The interface language.</param>
    /// <param name="ids">The source of the ids of the installed content.</param>
    /// <param name="profileState">The profile state of the panel: Auto or Fixed and the view (PER-007).</param>
    /// <param name="accepted">
    /// The profile was installed: the panel applies the transition to its session and shows the notice with Undo.
    /// </param>
    /// <param name="notify">Shows a failure in the notice bar.</param>
    public SuggestionViewModel(
        DocumentStore store,
        StarterContent? content,
        ILocalizationContext localization,
        IIdGenerator ids,
        Func<ProfileState> profileState,
        Action<SuggestionAccepted> accepted,
        Action<Message> notify
    )
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(profileState);
        ArgumentNullException.ThrowIfNull(accepted);
        ArgumentNullException.ThrowIfNull(notify);
        _store = store;
        _content = content;
        _localization = localization;
        _ids = ids;
        _profileState = profileState;
        _accepted = accepted;
        _notify = notify;
        Relocalize();
    }

    /// <summary>Whether the card is shown.</summary>
    public bool IsVisible => _suggestion is not null;

    /// <summary>The suggestion shown, or null.</summary>
    public ProfileSuggestion? Suggestion => _suggestion;

    /// <summary>The app name, in bold before <see cref="Message"/> (the template name in the interface language).</summary>
    public string AppName
    {
        get => _appName;
        private set => SetProperty(ref _appName, value);
    }

    /// <summary>[suggest]: «no tiene perfil. ¿Creo uno con atajos listos?».</summary>
    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    /// <summary>[create], the main button.</summary>
    public string CreateText
    {
        get => _createText;
        private set => SetProperty(ref _createText, value);
    }

    /// <summary>[notNow].</summary>
    public string NotNowText
    {
        get => _notNowText;
        private set => SetProperty(ref _notNowText, value);
    }

    /// <summary>
    /// Evaluates the card again: on every app switch, document change (a profile created or bound, «Detectar» changed)
    /// and when a search with text starts or ends.
    /// </summary>
    /// <param name="app">The executable of the app in front; empty when unknown.</param>
    /// <param name="searching">Whether the panel shows a search with text (PAN-008 hides the card).</param>
    public void Apply(ProcessName app, bool searching)
    {
        _app = app;
        _searching = searching;
        Evaluate();
    }

    /// <summary>[Crear perfil]: installs the template with undo and, in Auto, shows the new profile (PER-007).</summary>
    public void Create()
    {
        if (_suggestion is not { } suggestion)
        {
            return;
        }

        var result = ProfileSuggestions.Accept(
            _store,
            suggestion,
            _profileState(),
            new LangCode(_localization.Current.Locale.Code),
            _ids
        );
        Evaluate();
        if (result.TryGetValue(out var accepted))
        {
            _accepted(accepted);
        }
        else
        {
            _notify(result.Failure.Message);
        }
    }

    /// <summary>[Ahora no]: no card for this app until Clícalo closes.</summary>
    public void NotNow()
    {
        if (_suggestion is not { } suggestion)
        {
            return;
        }

        _dismissed = _dismissed.Add(suggestion.Process);
        Evaluate();
    }

    /// <summary>Formats every text again in the current language (IDI-001).</summary>
    public void Relocalize()
    {
        var localizer = _localization.Current;
        Message = localizer.Format(L.Suggest);
        CreateText = localizer.Format(L.Create);
        NotNowText = localizer.Format(L.NotNow);
        AppName =
            _suggestion?.Template.Name.Get(new LangCode(localizer.Locale.Code), LangCode.Es)
            ?? string.Empty;
    }

    private void Evaluate()
    {
        var document = _store.Current;
        var next = ProfileSuggestions.For(
            _app,
            document.Library,
            _content,
            document.Settings.AutoSuggestProfiles,
            _dismissed,
            _searching
        );
        if (Equals(next, _suggestion))
        {
            return;
        }

        _suggestion = next;
        Relocalize();
        OnPropertyChanged(nameof(Suggestion));
        OnPropertyChanged(nameof(IsVisible));
    }
}
