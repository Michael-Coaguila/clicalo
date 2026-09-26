using System;
using System.Collections.Generic;
using System.Linq;

namespace Clicalo.Generators.Localization;

/// <summary>All the entries of one base key in one language: a plain text or a plural family.</summary>
internal sealed class StringFamily(string baseKey)
{
    private readonly List<StringForm> _forms = [];

    public string BaseKey { get; } = baseKey;

    /// <summary>Entries in file order.</summary>
    public IReadOnlyList<StringForm> Forms => _forms;

    /// <summary>The first entry of the family, where family-level issues are reported.</summary>
    public StringForm First => _forms[0];

    public bool HasPlain => _forms.Exists(static f => f.Category is null);

    public bool HasPluralForms => _forms.Exists(static f => f.Category is not null);

    /// <summary>Plural categories present, in canonical CLDR order.</summary>
    public IEnumerable<string> Categories =>
        _forms
            .Where(static f => f.Category is not null)
            .Select(static f => f.Category!)
            .OrderBy(PluralCategories.OrderOf);

    /// <summary>Distinct placeholder names used by any entry of the family, ordinal-sorted.</summary>
    public IReadOnlyList<string> PlaceholderNames =>
        _forms
            .SelectMany(static f => f.Placeholders)
            .Select(static p => p.Name)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static n => n, StringComparer.Ordinal)
            .ToList();

    public void Add(StringForm form) => _forms.Add(form);

    /// <summary>The form of a category, or the plain text when <paramref name="category"/> is <c>null</c>.</summary>
    public StringForm? FormOf(string? category) =>
        _forms.Find(f => string.Equals(f.Category, category, StringComparison.Ordinal));
}
