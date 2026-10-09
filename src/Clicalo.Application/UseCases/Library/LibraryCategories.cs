using System.Collections.Immutable;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Templates;

namespace Clicalo.Application.UseCases.Library;

/// <summary>
/// The category chips of «Añadir atajo» for the list in view (ATJ-010): «[lcFor] {perfil}» first when the profile has
/// a template (and it is not Always visible), then the sections of the library in their order. Pure.
/// </summary>
public static class LibraryCategories
{
    /// <summary>The id of the «Para {perfil}» category.</summary>
    public const string ProfileCategory = "prof";

    private static readonly ImmutableDictionary<string, (Message Label, string Icon)> Sections =
        new Dictionary<string, (Message, string)>(StringComparer.Ordinal)
        {
            ["edit"] = (L.LcEdit, "edit"),
            ["win"] = (L.LcWin, "web_asset"),
            ["mouse"] = (L.LcMouse, "mouse"),
            ["voice"] = (L.LcVoice, "mic"),
            ["text"] = (L.LcText, "text_fields"),
            ["sys"] = (L.LcSys, "computer"),
        }.ToImmutableDictionary(StringComparer.Ordinal);

    /// <summary>The categories for <paramref name="list"/>, in chip order.</summary>
    /// <param name="library">The shortcuts of the document.</param>
    /// <param name="list">The list in view.</param>
    /// <param name="content">The library.</param>
    /// <param name="starter">The starter content (seed and templates); null when it could not be read.</param>
    /// <param name="language">The interface language, for the name of the profile.</param>
    public static ImmutableArray<LibraryCategory> For(
        ShortcutLibrary library,
        ListRef list,
        LibraryContent content,
        StarterContent? starter,
        LangCode language
    )
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(content);
        var categories = ImmutableArray.CreateBuilder<LibraryCategory>();
        if (
            list is ListRef.InProfile inProfile
            && library.TryGetProfile(inProfile.Id, out var profile)
            && OwnCategory(profile, starter, language) is { } own
        )
        {
            categories.Add(own);
        }

        foreach (var section in content.Sections)
        {
            if (Sections.TryGetValue(section.Id, out var look) && !section.Shortcuts.IsEmpty)
            {
                categories.Add(
                    new LibraryCategory(
                        section.Id,
                        look.Label,
                        look.Icon,
                        section.Shortcuts,
                        LibraryContent.Source,
                        content.Version
                    )
                );
            }
        }

        return categories.ToImmutable();
    }

    private static LibraryCategory? OwnCategory(
        Profile profile,
        StarterContent? starter,
        LangCode language
    )
    {
        if (starter is null)
        {
            return null;
        }

        var label = L.LcFor(profile: profile.Name.Get(language, LangCode.Es));
        if (profile.Id == ProfileId.General)
        {
            return starter.Seed.General.IsEmpty
                ? null
                : new LibraryCategory(
                    ProfileCategory,
                    label,
                    profile.Icon.Name,
                    starter.Seed.General,
                    SeedContent.Source,
                    starter.Seed.CatalogVersion
                );
        }

        var template =
            (profile.Origin is { } origin ? starter.Template(origin.Source) : null)
            ?? ProcessesOf(profile.Binding)
                .Select(starter.TemplateFor)
                .FirstOrDefault(t => t is not null);
        return template is null || template.Shortcuts.IsEmpty
            ? null
            : new LibraryCategory(
                ProfileCategory,
                label,
                profile.Icon.Name,
                template.Shortcuts,
                template.Id,
                template.Version
            );
    }

    private static System.Collections.Immutable.ImmutableArray<ProcessName> ProcessesOf(
        AppBinding binding
    ) => binding is AppBinding.Processes bound ? bound.Names.Items : [];
}
