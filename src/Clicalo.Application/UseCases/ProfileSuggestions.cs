using System.Collections.Immutable;
using Clicalo.Application.Store;
using Clicalo.Domain.Commands;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.ProfileResolution;
using Clicalo.Domain.Templates;

namespace Clicalo.Application.UseCases;

/// <summary>
/// The profile suggestion of the panel (PER-009, docs/04 §6, EC-PER-02): when to offer it and what «Crear perfil»
/// does. The panel keeps the apps dismissed with «Ahora no» for the session and passes them in; nothing here is state.
/// </summary>
public static class ProfileSuggestions
{
    /// <summary>
    /// The suggestion for the app in front, or <see langword="null"/>. It is offered only when all hold: the app has no
    /// profile, «Detectar» (<c>autoSuggestProfiles</c>) is on, the app was not dismissed in this session, no search with
    /// text is shown (PAN-008) and a template lists its process among its own (ADR-0021). Without a template there is
    /// nothing (EC-PER-02, PQ-14).
    /// </summary>
    /// <param name="app">The executable of the verified external foreground; empty when unknown.</param>
    /// <param name="library">The shortcuts and profiles of the document.</param>
    /// <param name="content">The templates that ship with Clícalo; null when they could not be read.</param>
    /// <param name="autoSuggest">The «Detectar» setting.</param>
    /// <param name="dismissed">The apps dismissed with «Ahora no» since Clícalo started.</param>
    /// <param name="searching">Whether the panel shows a search with text.</param>
    public static ProfileSuggestion? For(
        ProcessName app,
        ShortcutLibrary library,
        StarterContent? content,
        bool autoSuggest,
        ImmutableHashSet<ProcessName> dismissed,
        bool searching
    )
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(dismissed);
        if (
            app.IsEmpty
            || !autoSuggest
            || searching
            || content is null
            || dismissed.Contains(app)
            || library.ProfileFor(app) is not null
        )
        {
            return null;
        }

        return content.TemplateFor(app) is { } template
            ? new ProfileSuggestion(app, template)
            : null;
    }

    /// <summary>
    /// «Crear perfil»: installs the template as a new profile, with the combinations of the programs language
    /// (PLA-009) and with undo, and says how the view follows: to the new profile only in Auto and outside Frequents
    /// (PER-007).
    /// </summary>
    /// <param name="store">The document store; the creation is one undoable step.</param>
    /// <param name="suggestion">The suggestion the card shows.</param>
    /// <param name="state">The profile state of the panel when the button was tapped.</param>
    /// <param name="uiLanguage">The interface language, for the app name of the notice.</param>
    /// <param name="ids">The source of the ids of the copied content (the store gives the final ones, DAT-004).</param>
    /// <returns>The new profile and the transition, or why nothing was installed.</returns>
    public static Result<SuggestionAccepted> Accept(
        DocumentStore store,
        ProfileSuggestion suggestion,
        ProfileState state,
        LangCode uiLanguage,
        IIdGenerator ids
    )
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(suggestion);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(ids);
        var document = store.Current;
        var library = document.Library;
        var profile = TemplateInstaller.CreateProfile(
            suggestion.Template,
            document.Settings.Keyboard.AppsLanguage,
            ids,
            process => library.ProfileFor(process) is not null
        );
        return store
            .Dispatch(new CreateProfile(profile, ListPosition.End))
            .Map(next =>
            {
                // The new profile binds the suggested process: it had no profile, so it was not taken.
                var created =
                    next.Library.ProfileFor(suggestion.Process)
                    ?? throw new InvalidOperationException(
                        "The installed template does not bind the suggested app."
                    );
                return new SuggestionAccepted(
                    created.Id,
                    ProfileResolver.OnTemplateInstalled(state, created.Id),
                    L.InstalledApp(app: suggestion.Template.Name.Get(uiLanguage, LangCode.Es))
                );
            });
    }
}
