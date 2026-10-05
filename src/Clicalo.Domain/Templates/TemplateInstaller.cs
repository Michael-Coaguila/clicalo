using System.Collections.Immutable;
using System.Globalization;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Templates;

/// <summary>
/// Copies content into the document (PLA-013, DAT-004): every shortcut and profile gets a new id and a catalog
/// reference to its source, and a tap is installed with the combination of the programs language (CAT-005). Pure.
/// </summary>
public static class TemplateInstaller
{
    /// <summary>
    /// A document shortcut from <paramref name="item"/>, with a new id. A tap is installed with the combination of
    /// <paramref name="appsLanguage"/> (its variant, or the content combination when it has none) and without variants:
    /// changing the programs language later does not change installed shortcuts (EC-PLA-04); the catalog reference lets
    /// the preview offer the other variant.
    /// </summary>
    /// <param name="item">The content shortcut.</param>
    /// <param name="source">The template id, or <see cref="SeedContent.Source"/>.</param>
    /// <param name="version">The version of the source.</param>
    /// <param name="appsLanguage">The programs language of the keyboard settings (PLA-009).</param>
    /// <param name="ids">The source of new ids.</param>
    public static Shortcut Install(
        TemplateShortcut item,
        string source,
        int version,
        LangCode appsLanguage,
        IIdGenerator ids
    )
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(ids);
        return new Shortcut(
            ids.NewShortcutId(),
            item.Name,
            item.Icon,
            false,
            item.Category,
            ForLanguage(item.Action, appsLanguage),
            new ShortcutOptions(item.Confirm, new HoldLimit.InheritGlobal(), false),
            new CatalogRef(source, Version(version), item.ItemId),
            null
        );
    }

    /// <summary>
    /// A new profile from <paramref name="template"/>, bound to every process of it that <paramref name="isTaken"/>
    /// does not report (a process belongs to one profile, invariant I5); manual when none is left.
    /// </summary>
    /// <param name="template">The template.</param>
    /// <param name="appsLanguage">The programs language of the keyboard settings (PLA-009).</param>
    /// <param name="ids">The source of new ids.</param>
    /// <param name="isTaken">Whether another profile already binds a process.</param>
    public static Profile CreateProfile(
        ProfileTemplate template,
        LangCode appsLanguage,
        IIdGenerator ids,
        Func<ProcessName, bool> isTaken
    )
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(isTaken);
        var processes = template
            .Processes.Items.Where(p => !p.IsEmpty && !isTaken(p))
            .Distinct()
            .ToImmutableArray();
        var id = ids.NewProfileId();
        return new Profile(
            id,
            template.Name,
            template.Icon,
            false,
            processes.IsEmpty
                ? new AppBinding.Manual()
                : new AppBinding.Processes(new ValueList<ProcessName>(processes)),
            InjectionMode.VirtualKey,
            [
                .. template.Shortcuts.Items.Select(item =>
                    Install(item, template.Id, template.Version, appsLanguage, ids)
                ),
            ],
            new CatalogRef(template.Id, Version(template.Version), template.Id)
        );
    }

    private static ShortcutAction ForLanguage(ShortcutAction action, LangCode appsLanguage)
    {
        if (action is not TapAction tap || tap.Variants.IsEmpty)
        {
            return action;
        }

        var chosen =
            tap.Variants.Items.FirstOrDefault(v => v.AppsLanguage == appsLanguage)?.Chord
            ?? tap.Chord;
        return new TapAction(chosen, []);
    }

    private static string Version(int version) => version.ToString(CultureInfo.InvariantCulture);
}
