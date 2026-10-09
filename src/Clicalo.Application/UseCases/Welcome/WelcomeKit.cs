using Clicalo.Domain.Errors;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Templates;

namespace Clicalo.Application.UseCases.Welcome;

/// <summary>
/// The starter kit of the welcome step «¿Qué apps usas más?» on a document that already exists (user decision D2,
/// BIE-006, BIE-010). Pure and only additive, so it never loses data (REG-08):
/// <list type="bullet">
/// <item>«Basics» adds the seed shortcuts of Always visible and General that the list does not have yet (by their
/// catalog reference), at the end;</item>
/// <item>each marked template that has no profile yet becomes a profile after the others, bound to its processes that
/// no profile binds (invariant I5), with the combinations of the programs language (PLA-013);</item>
/// <item>an unmarked option removes nothing: unmarking an installed template or «Basics» does not uninstall it.</item>
/// </list>
/// </summary>
public static class WelcomeKit
{
    /// <summary>
    /// The options already in <paramref name="library"/>: «Basics» when Always visible or General hold a seed
    /// shortcut, and each template with a profile made from it. A repeated welcome shows them marked (BIE-010).
    /// </summary>
    /// <param name="library">The library.</param>
    /// <param name="content">The kit, the seed and the templates.</param>
    public static StarterSelection Installed(ShortcutLibrary library, StarterContent content)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(content);
        var installed = new List<string>();
        foreach (var option in content.Kit.Options)
        {
            var present = option.Kind switch
            {
                StarterOptionKind.Basics => library.AlwaysVisible.Items.Any(FromSeed)
                    || library.General.Shortcuts.Items.Any(FromSeed),
                StarterOptionKind.Template => HasTemplate(library, option.Id),
                _ => false,
            };
            if (present)
            {
                installed.Add(option.Id);
            }
        }

        return StarterSelection.Of(installed);
    }

    /// <summary>The library with what <paramref name="selection"/> marks and it does not have yet.</summary>
    /// <param name="library">The library.</param>
    /// <param name="content">The kit, the seed and the templates.</param>
    /// <param name="selection">The marked options.</param>
    /// <param name="appsLanguage">The programs language (PLA-009).</param>
    /// <param name="ids">The source of new ids; ids already in the library are skipped (DAT-004).</param>
    public static Result<ShortcutLibrary> Install(
        ShortcutLibrary library,
        StarterContent content,
        StarterSelection selection,
        LangCode appsLanguage,
        IIdGenerator ids
    )
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(ids);
        var fresh = new FreshIds(library, ids);
        var alwaysVisible = library.AlwaysVisible.Items.ToList();
        var profiles = library.Profiles.Items.ToList();
        var basics = content.Kit.Options.Items.Any(o =>
            o.Kind == StarterOptionKind.Basics && selection.IsChosen(o.Id)
        );
        if (basics)
        {
            var seed = content.Seed;
            alwaysVisible.AddRange(
                Missing(seed.AlwaysVisible, alwaysVisible, seed, appsLanguage, fresh)
            );
            var general = profiles.FindIndex(p => p.Id == ProfileId.General);
            if (general >= 0)
            {
                var shortcuts = profiles[general].Shortcuts.Items.ToList();
                shortcuts.AddRange(Missing(seed.General, shortcuts, seed, appsLanguage, fresh));
                profiles[general] = profiles[general] with { Shortcuts = [.. shortcuts] };
            }
        }

        var bound = new HashSet<ProcessName>(
            profiles.SelectMany(p =>
                p.Binding is AppBinding.Processes processes ? processes.Names.Items : []
            )
        );
        foreach (var option in content.Kit.Options)
        {
            if (
                option.Kind != StarterOptionKind.Template
                || !selection.IsChosen(option.Id)
                || HasTemplate(library, option.Id)
                || content.Template(option.Id) is not { } template
            )
            {
                continue;
            }

            var profile = TemplateInstaller.CreateProfile(
                template,
                appsLanguage,
                fresh,
                bound.Contains
            );
            if (profile.Binding is AppBinding.Processes processes)
            {
                bound.UnionWith(processes.Names.Items);
            }

            profiles.Add(profile);
        }

        return ShortcutLibrary.CreateValidated([.. alwaysVisible], [.. profiles]);
    }

    private static bool FromSeed(Shortcut shortcut) =>
        shortcut.Origin is { } origin
        && string.Equals(origin.Source, SeedContent.Source, StringComparison.Ordinal);

    private static bool HasTemplate(ShortcutLibrary library, string templateId) =>
        library.Profiles.Items.Any(p =>
            p.Origin is { } origin
            && string.Equals(origin.Source, templateId, StringComparison.Ordinal)
        );

    private static List<Shortcut> Missing(
        ValueList<TemplateShortcut> items,
        List<Shortcut> list,
        SeedContent seed,
        LangCode appsLanguage,
        IIdGenerator ids
    )
    {
        var present = list.Where(FromSeed)
            .Select(s => s.Origin!.Value.ItemId)
            .ToHashSet(StringComparer.Ordinal);
        return items
            .Items.Where(item => !present.Contains(item.ItemId))
            .Select(item =>
                TemplateInstaller.Install(
                    item,
                    SeedContent.Source,
                    seed.CatalogVersion,
                    appsLanguage,
                    ids
                )
            )
            .ToList();
    }

    /// <summary>New ids that the library does not use yet.</summary>
    private sealed class FreshIds(ShortcutLibrary library, IIdGenerator inner) : IIdGenerator
    {
        private const int Attempts = 64;
        private readonly HashSet<string> _taken = new(StringComparer.Ordinal);

        public ProfileId NewProfileId() => Next(inner.NewProfileId, id => id.Value);

        public ShortcutId NewShortcutId() => Next(inner.NewShortcutId, id => id.Value);

        private T Next<T>(Func<T> create, Func<T, string> value)
        {
            for (var attempt = 0; attempt < Attempts; attempt++)
            {
                var id = create();
                var text = value(id);
                if (!string.IsNullOrEmpty(text) && !library.ContainsId(text) && _taken.Add(text))
                {
                    return id;
                }
            }

            throw new InvalidOperationException("The id generator keeps returning ids in use.");
        }
    }
}
