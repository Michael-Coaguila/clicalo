using System.Collections.Immutable;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Icons.Internal;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Icons;

/// <summary>
/// The icon library of the editor (<c>data/catalogs/icons.json</c>): every icon with its search words, the 24 featured
/// icons the picker shows first (the prototype's <c>ICONS</c>), the 28 of a profile, and the default icons of a new
/// shortcut (<c>bolt</c>) and a new profile (<c>apps</c>) (EDI-004, EDI-005, ATJ-004). Immutable; content is
/// untrusted, so Infrastructure validates the file before building it.
/// </summary>
public sealed class IconCatalog
{
    /// <summary>The icon of a new shortcut without a suggestion (EDI-005).</summary>
    public static readonly IconRef ShortcutDefault = new("bolt");

    /// <summary>The icon of a new profile without a suggestion (EDI-005).</summary>
    public static readonly IconRef ProfileDefault = new("apps");

    private readonly ImmutableArray<string> _haystacks;

    /// <summary>Creates the catalog.</summary>
    /// <param name="entries">Every icon with its words, in the order of the file.</param>
    /// <param name="featured">The icons the picker shows first.</param>
    /// <param name="profileFeatured">The icons of the profile picker.</param>
    public IconCatalog(
        IEnumerable<IconEntry> entries,
        IEnumerable<IconRef> featured,
        IEnumerable<IconRef> profileFeatured
    )
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(featured);
        ArgumentNullException.ThrowIfNull(profileFeatured);
        var seen = new HashSet<IconRef>();
        var list = ImmutableArray.CreateBuilder<IconEntry>();
        foreach (var entry in entries)
        {
            if (entry is not null && !string.IsNullOrEmpty(entry.Icon.Name) && seen.Add(entry.Icon))
            {
                list.Add(entry);
            }
        }

        Entries = new ValueList<IconEntry>(list.ToImmutable());
        Featured = Distinct(featured);
        ProfileFeatured = Distinct(profileFeatured);
        _haystacks = [.. Entries.Items.Select(Haystack)];
    }

    /// <summary>A catalog without icons: only the defaults are offered.</summary>
    public static IconCatalog Empty { get; } = new([], [ShortcutDefault], [ProfileDefault]);

    /// <summary>Every icon with its words, in the order of the file (the order of the suggestions, EDI-005).</summary>
    public ValueList<IconEntry> Entries { get; }

    /// <summary>The icons the picker shows first without a search (EDI-004).</summary>
    public ValueList<IconRef> Featured { get; }

    /// <summary>The icons of the profile editor (ATJ-004: «el actual más 28 de base»).</summary>
    public ValueList<IconRef> ProfileFeatured { get; }

    /// <summary>
    /// The icons of the picker without a search (EDI-004): the featured ones first, then the rest of the library in
    /// its order, each once.
    /// </summary>
    public ValueList<IconRef> Browse()
    {
        var seen = new HashSet<IconRef>();
        var result = ImmutableArray.CreateBuilder<IconRef>();
        foreach (var icon in Featured.Items.Concat(Entries.Items.Select(static e => e.Icon)))
        {
            if (seen.Add(icon))
            {
                result.Add(icon);
            }
        }

        return new ValueList<IconRef>(result.ToImmutable());
    }

    /// <summary>
    /// The icons whose words or name contain <paramref name="query"/> without case and without diacritics (EDI-004:
    /// «guardar», «voz», «pdf»), in the order of the library; an empty query gives <see cref="Browse"/>.
    /// </summary>
    /// <param name="query">What the person typed.</param>
    public ValueList<IconRef> Search(string? query)
    {
        var folded = IconText.Fold(query).Trim();
        if (folded.Length == 0)
        {
            return Browse();
        }

        var result = ImmutableArray.CreateBuilder<IconRef>();
        for (var i = 0; i < Entries.Count; i++)
        {
            if (_haystacks[i].Contains(folded, StringComparison.Ordinal))
            {
                result.Add(Entries[i].Icon);
            }
        }

        return new ValueList<IconRef>(result.ToImmutable());
    }

    private static string Haystack(IconEntry entry) =>
        IconText.Fold(string.Join(' ', entry.Tags.Items) + " " + entry.Icon.Name);

    private static ValueList<IconRef> Distinct(IEnumerable<IconRef> icons) =>
        new([.. icons.Where(static icon => !string.IsNullOrEmpty(icon.Name)).Distinct()]);
}
