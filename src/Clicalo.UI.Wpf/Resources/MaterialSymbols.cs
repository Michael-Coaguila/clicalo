using System.Collections.Frozen;
using System.Globalization;
using System.Text.Json;

namespace Clicalo.UI.Wpf.Resources;

/// <summary>
/// The code point of every bundled Material Symbols icon, by its Material name (<c>assets/fonts/codepoints.json</c>,
/// TEM-005). The bundled fonts have no ligatures: an icon is drawn by its code point, never by typing its name, so a
/// name never reaches the screen or UI Automation as text (UIA008).
/// </summary>
/// <remarks>The table is read once from the assembly and is immutable: any thread can use it.</remarks>
public static class MaterialSymbols
{
    private const string ResourceName = "Clicalo.UI.Wpf.Resources.codepoints.json";

    private static readonly Lazy<FrozenDictionary<string, int>> Table = new(
        Load,
        LazyThreadSafetyMode.ExecutionAndPublication
    );

    /// <summary>
    /// The icon drawn for a name that the bundled font does not have (TEM-005: «hay un icono de respaldo»): the
    /// default icon of a shortcut (<c>icons.json</c> → <c>defaults.shortcut</c>).
    /// </summary>
    public const string FallbackName = "bolt";

    /// <summary>Every bundled icon name.</summary>
    public static IReadOnlyCollection<string> Names => Table.Value.Keys;

    /// <summary>True when the bundled fonts draw <paramref name="name"/>.</summary>
    public static bool Contains(string? name) => name is not null && Table.Value.ContainsKey(name);

    /// <summary>The code point of <paramref name="name"/>, if bundled.</summary>
    public static bool TryGetCodePoint(string? name, out int codePoint)
    {
        codePoint = 0;
        return name is not null && Table.Value.TryGetValue(name, out codePoint);
    }

    /// <summary>
    /// The code point of <paramref name="name"/>, or of <see cref="FallbackName"/> when the bundled fonts do not
    /// have it (an icon name from an imported profile, for example).
    /// </summary>
    public static int CodePointOrFallback(string? name) =>
        TryGetCodePoint(name, out var codePoint) ? codePoint : Table.Value[FallbackName];

    private static FrozenDictionary<string, int> Load()
    {
        using var stream =
            typeof(MaterialSymbols).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"The resource {ResourceName} is missing from the assembly."
            );
        using var document = JsonDocument.Parse(stream);
        var icons = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var icon in document.RootElement.GetProperty("icons").EnumerateObject())
        {
            icons.Add(
                icon.Name,
                int.Parse(
                    icon.Value.GetString() ?? string.Empty,
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture
                )
            );
        }

        return icons.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
