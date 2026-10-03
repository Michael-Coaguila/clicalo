using Clicalo.Domain.Errors;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Templates;

/// <summary>
/// The library of a new document from the starter kit and a selection (user decision D2 of 2026-10-03, BIE-006,
/// CAT-003). Pure:
/// <list type="bullet">
/// <item>General and the Always visible row always exist; «Basics» fills them with the seed, and without it they are
/// empty (nothing marked starts empty).</item>
/// <item>Each marked template becomes a profile after General, in the order of the kit, bound to its processes, with
/// the combinations of the programs language (PLA-013, CAT-005). A process already bound by an earlier profile is left
/// out of the later one (invariant I5).</item>
/// <item>Every shortcut and profile gets a new id and a catalog reference (DAT-004).</item>
/// </list>
/// </summary>
public static class StarterLibrary
{
    /// <summary>Builds the library; fails only if the content breaks an invariant of the library.</summary>
    /// <param name="content">The kit, the seed and the templates.</param>
    /// <param name="selection">
    /// The marked options; an id the kit does not have, or a template it lacks, adds nothing.
    /// </param>
    /// <param name="appsLanguage">The programs language of the keyboard settings (PLA-009).</param>
    /// <param name="ids">The source of new ids.</param>
    public static Result<ShortcutLibrary> Build(
        StarterContent content,
        StarterSelection selection,
        LangCode appsLanguage,
        IIdGenerator ids
    )
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(ids);
        var seed = content.Seed;
        var basics = content.Kit.Options.Items.Any(o =>
            o.Kind == StarterOptionKind.Basics && selection.IsChosen(o.Id)
        );
        ValueList<Shortcut> alwaysVisible = basics
            ? Seeded(seed, seed.AlwaysVisible, appsLanguage, ids)
            : [];
        var profiles = new List<Profile>
        {
            new(
                ProfileId.General,
                seed.GeneralName,
                seed.GeneralIcon,
                false,
                new AppBinding.Manual(),
                InjectionMode.VirtualKey,
                basics ? Seeded(seed, seed.General, appsLanguage, ids) : [],
                null
            ),
        };
        var bound = new HashSet<ProcessName>();
        foreach (var option in content.Kit.Options)
        {
            if (
                option.Kind != StarterOptionKind.Template
                || !selection.IsChosen(option.Id)
                || content.Template(option.Id) is not { } template
            )
            {
                continue;
            }

            var profile = TemplateInstaller.CreateProfile(
                template,
                appsLanguage,
                ids,
                bound.Contains
            );
            if (profile.Binding is AppBinding.Processes processes)
            {
                bound.UnionWith(processes.Names);
            }

            profiles.Add(profile);
        }

        return ShortcutLibrary.CreateValidated(alwaysVisible, [.. profiles]);
    }

    private static ValueList<Shortcut> Seeded(
        SeedContent seed,
        ValueList<TemplateShortcut> items,
        LangCode appsLanguage,
        IIdGenerator ids
    ) =>
        [
            .. items.Items.Select(item =>
                TemplateInstaller.Install(
                    item,
                    SeedContent.Source,
                    seed.CatalogVersion,
                    appsLanguage,
                    ids
                )
            ),
        ];
}
