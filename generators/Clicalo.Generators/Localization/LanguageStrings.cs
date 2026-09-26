using System;
using System.Collections.Generic;

namespace Clicalo.Generators.Localization;

/// <summary>The parsed strings file of one language, grouped by base key in file order.</summary>
internal sealed class LanguageStrings(string code, string path)
{
    private readonly Dictionary<string, StringFamily> _families = new(StringComparer.Ordinal);
    private readonly List<StringFamily> _ordered = [];

    public string Code { get; } = code;

    public string Path { get; } = path;

    /// <summary>Families in the order their first entry appears in the file.</summary>
    public IReadOnlyList<StringFamily> Families => _ordered;

    public bool TryGet(string baseKey, out StringFamily family) =>
        _families.TryGetValue(baseKey, out family!);

    public void Add(StringForm form)
    {
        if (!_families.TryGetValue(form.BaseKey, out var family))
        {
            family = new StringFamily(form.BaseKey);
            _families.Add(form.BaseKey, family);
            _ordered.Add(family);
        }

        family.Add(form);
    }
}
