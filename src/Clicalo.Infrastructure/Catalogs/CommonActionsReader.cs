using System.Text.Json;
using Clicalo.Domain.CommonActions;
using Clicalo.Domain.Primitives;

namespace Clicalo.Infrastructure.Catalogs;

/// <summary>
/// Reads <c>data/catalogs/common-actions.json</c> (decision D4): the origins, the app families and, for each common
/// action, its standard combination and its exceptions with the processes of their families. Untrusted data (LOG-006):
/// any key outside the catalog, unknown family or empty field rejects the whole file (<see langword="null"/>), so the
/// engine then sends every shortcut as saved.
/// </summary>
public static class CommonActionsReader
{
    /// <summary>The file name inside the catalogs folder.</summary>
    public const string FileName = "common-actions.json";

    /// <summary>The table of <paramref name="json"/>, or <see langword="null"/> when it breaks a rule.</summary>
    /// <param name="json">The bytes of <c>common-actions.json</c>.</param>
    public static CommonActionTable? Read(ReadOnlyMemory<byte> json) =>
        ContentJson.Read(json, ReadTable);

    private static CommonActionTable ReadTable(JsonElement root)
    {
        var origins = root.GetProperty("origins")
            .EnumerateArray()
            .Select(static o => o.GetString() ?? throw new FormatException("Empty origin."))
            .ToList();
        var families = new Dictionary<string, List<ProcessName>>(StringComparer.Ordinal);
        foreach (var family in root.GetProperty("families").EnumerateArray())
        {
            var processes = family
                .GetProperty("processes")
                .EnumerateArray()
                .Select(static p => new ProcessName(p.GetString() ?? string.Empty))
                .ToList();
            if (processes.Count == 0 || processes.Any(static p => p.IsEmpty))
            {
                throw new FormatException("A family has no process.");
            }

            families.Add(ContentJson.String(family, "id"), processes);
        }

        var actions = new List<CommonAction>();
        foreach (var action in root.GetProperty("actions").EnumerateArray())
        {
            var overrides = new List<CommonActionOverride>();
            foreach (var exception in action.GetProperty("exceptions").EnumerateArray())
            {
                var processes = exception
                    .GetProperty("families")
                    .EnumerateArray()
                    .SelectMany(f =>
                        families[f.GetString() ?? throw new FormatException("Empty family.")]
                    )
                    .Distinct()
                    .ToList();
                overrides.Add(
                    new CommonActionOverride(
                        [.. processes],
                        new LangCode(ContentJson.String(exception, "appsLanguage")),
                        ChordOf(exception)
                    )
                );
            }

            actions.Add(
                new CommonAction(ContentJson.String(action, "id"), ChordOf(action), [.. overrides])
            );
        }

        return new CommonActionTable(origins, actions);
    }

    private static Domain.Keys.KeyChord ChordOf(JsonElement element) =>
        ContentJson.Chord(element.GetProperty("keys"))
        ?? throw new FormatException("A combination is empty or has a key outside the catalog.");
}
