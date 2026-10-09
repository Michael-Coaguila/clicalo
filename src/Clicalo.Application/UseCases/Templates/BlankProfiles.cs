using Clicalo.Application.Store;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Commands;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;

namespace Clicalo.Application.UseCases.Templates;

/// <summary>
/// «Perfil vacío» of Plantillas (PLA-010) and [unknownBlank] (PLA-007): a profile without shortcuts, with the same name
/// in every language, at the end of the order, in one undoable step with [profCreated].
/// </summary>
public static class BlankProfiles
{
    /// <summary>Creates the profile.</summary>
    /// <param name="store">The document.</param>
    /// <param name="name">The name typed or dictated; an empty name creates nothing.</param>
    /// <param name="icon">The icon shown in the form.</param>
    /// <param name="autoIcon">Whether the icon still follows the name.</param>
    /// <param name="process">
    /// The open app chosen in «Se activa con», bound at once when no other profile has it; null for capture or none.
    /// </param>
    /// <returns>The new profile.</returns>
    public static Result<ProfileId> Create(
        DocumentStore store,
        string name,
        IconRef icon,
        bool autoIcon,
        ProcessName? process
    )
    {
        ArgumentNullException.ThrowIfNull(store);
        var text = (name ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return Results.Fail<ProfileId>(TemplateFailures.NoName());
        }

        var free = process is { IsEmpty: false } p && store.Current.Library.ProfileFor(p) is null;
        var profile = new Profile(
            new ProfileId("blank"),
            LocalizedText.Same(text, LangCode.Es, LangCode.En),
            icon,
            autoIcon,
            free ? new AppBinding.Processes([process!.Value]) : new AppBinding.Manual(),
            InjectionMode.VirtualKey,
            [],
            null
        );
        return store
            .Dispatch(new CreateProfile(profile, ListPosition.End))
            .Map(next => next.Library.Profiles[next.Library.Profiles.Count - 1].Id);
    }
}
