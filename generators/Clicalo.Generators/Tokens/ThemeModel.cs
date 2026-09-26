using System;
using System.Collections.Generic;

namespace Clicalo.Generators.Tokens;

/// <summary>One theme with every color resolved to what is rendered.</summary>
internal sealed class ThemeModel(
    string key,
    string name,
    bool isHighContrast,
    DataPosition position
)
{
    /// <summary>Key in the data, for example <c>hc</c>.</summary>
    public string Key { get; } = key;

    /// <summary>C# name, for example <c>HighContrast</c>.</summary>
    public string Name { get; } = name;

    public bool IsHighContrast { get; } = isHighContrast;

    public DataPosition Position { get; } = position;

    /// <summary>Width of the <c>border</c> shorthand, in device-independent pixels.</summary>
    public double BorderThickness { get; set; } = 1d;

    public Dictionary<string, ResolvedColor> Colors { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, ResolvedColor> CategoryTints { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, ResolvedColor> CategoryWashes { get; } = new(StringComparer.Ordinal);
}
