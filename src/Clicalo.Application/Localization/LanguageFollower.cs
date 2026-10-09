using System.Collections.Immutable;
using Clicalo.Application.Store;
using Clicalo.Domain.Commands;
using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;

namespace Clicalo.Application.Localization;

/// <summary>
/// The hot language switch of every window (IDI-001): the document's <c>lang</c> setting is the single source; when it
/// changes, the <see cref="ILocalizationContext"/> switches, and every registered window formats its texts again on its
/// own thread. Nothing restarts and no field loses what is being typed: windows repaint their texts, they are not
/// rebuilt.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>A window registers once (<see cref="Register"/>) with how it repaints and how to reach its thread (a
/// dispatcher's <c>BeginInvoke</c>); the switch may happen on any thread.</item>
/// <item>The language selector of the control center (CCM-001) and any other control choose a language with
/// <see cref="Choose"/>, which only writes the setting: the document change does the rest, so undoing it or loading
/// another document switches the same way.</item>
/// </list>
/// </remarks>
public sealed class LanguageFollower : IDisposable
{
    private readonly ILocalizationContext _localization;
    private readonly DocumentStore _store;
    private readonly Lock _gate = new();
    private ImmutableArray<Registration> _windows = [];
    private bool _disposed;

    /// <summary>Creates the follower and switches to the document's language at once.</summary>
    /// <param name="localization">The interface language.</param>
    /// <param name="store">The document whose <c>lang</c> setting rules.</param>
    public LanguageFollower(ILocalizationContext localization, DocumentStore store)
    {
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(store);
        _localization = localization;
        _store = store;
        _store.Changed += OnDocumentChanged;
        _localization.LanguageChanged += OnLanguageChanged;
        _ = _localization.TrySetLanguage(store.Current.Settings.Language.Value);
    }

    /// <summary>
    /// Chooses the interface language: writes the <c>lang</c> setting (a presentation setting, outside the undo
    /// history, DAT-006), and the change switches every window.
    /// </summary>
    /// <param name="language">One of <see cref="ILocalizationContext.Languages"/>.</param>
    public Result<UserDocument> Choose(LangCode language) =>
        _store.Dispatch(new SetSetting(SettingPaths.Language, language));

    /// <summary>
    /// Registers a window: after every switch, <paramref name="post"/> runs <paramref name="relocalize"/> on the
    /// window's thread. Dispose the result when the window closes.
    /// </summary>
    /// <param name="relocalize">Formats every text of the window again (its view models' <c>Relocalize</c>).</param>
    /// <param name="post">Runs an action on the window's thread without waiting for it.</param>
    public IDisposable Register(Action relocalize, Action<Action> post)
    {
        ArgumentNullException.ThrowIfNull(relocalize);
        ArgumentNullException.ThrowIfNull(post);
        var registration = new Registration(this, relocalize, post);
        lock (_gate)
        {
            _windows = _windows.Add(registration);
        }

        return registration;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _store.Changed -= OnDocumentChanged;
        _localization.LanguageChanged -= OnLanguageChanged;
        lock (_gate)
        {
            _windows = [];
        }
    }

    private void OnDocumentChanged(object? sender, DocumentChangedEventArgs change)
    {
        var language = change.After.Settings.Language;
        if (language != change.Before.Settings.Language)
        {
            _ = _localization.TrySetLanguage(language.Value);
        }
    }

    private void OnLanguageChanged(object? sender, LanguageChangedEventArgs change)
    {
        ImmutableArray<Registration> windows;
        lock (_gate)
        {
            windows = _windows;
        }

        foreach (var window in windows)
        {
            window.Post(window.Relocalize);
        }
    }

    private void Unregister(Registration registration)
    {
        lock (_gate)
        {
            _windows = _windows.Remove(registration);
        }
    }

    private sealed class Registration(
        LanguageFollower owner,
        Action relocalize,
        Action<Action> post
    ) : IDisposable
    {
        public Action Relocalize { get; } = relocalize;

        public Action<Action> Post { get; } = post;

        public void Dispose() => owner.Unregister(this);
    }
}
