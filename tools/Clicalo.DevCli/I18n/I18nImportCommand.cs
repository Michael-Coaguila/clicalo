using System.Globalization;
using System.Text;
using System.Text.Json;
using Clicalo.Generators.Localization;

namespace Clicalo.DevCli.I18n;

/// <summary>
/// <c>cl i18n-import</c>: rebuilds <c>data/i18n/strings.*.json</c> from the design handoff with the reviewed recipe
/// <c>data/i18n/handoff-import.json</c>, then validates the result like the build. With <c>--check</c> it writes
/// nothing and fails when <c>data/i18n</c> differs from a fresh import.
/// </summary>
internal static class I18nImportCommand
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static int Run(string root, bool check, TextWriter output)
    {
        var report = new I18nReport(root, output);
        HandoffRecipe recipe;
        try
        {
            recipe = HandoffRecipe.Parse(File.ReadAllText(I18nPaths.Recipe(root)));
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException)
        {
            output.WriteLine("error: " + ex.Message);
            output.WriteLine("i18n-import: the recipe could not be read.");
            return ExitCodes.Failure;
        }

        var handoff = new Dictionary<string, IReadOnlyList<KeyValuePair<string, string>>>(
            StringComparer.Ordinal
        );
        foreach (var language in recipe.Languages)
        {
            var path = Path.Combine(
                root,
                recipe.Source,
                LocalizationFiles.StringsFileName(language)
            );
            var entries = ReadFlat(path, report);
            if (entries is not null)
            {
                handoff[language] = entries;
            }
        }

        var import = HandoffImporter.Run(recipe, handoff);
        foreach (var error in import.Errors)
        {
            report.Add(I18nCheckIds.ImportFailed, error, I18nPaths.Recipe(root), 0, 0);
        }

        if (report.Errors > 0)
        {
            output.WriteLine("i18n-import: nothing written; fix the problems above.");
            return ExitCodes.Failure;
        }

        foreach (var language in recipe.Languages)
        {
            var path = I18nPaths.Strings(root, language);
            var content = FlatJsonWriter.Write(import.Entries[language]);
            if (!check)
            {
                File.WriteAllText(path, content, Utf8NoBom);
            }
            else if (
                !File.Exists(path)
                || !string.Equals(File.ReadAllText(path), content, StringComparison.Ordinal)
            )
            {
                report.Add(
                    I18nCheckIds.ImportFailed,
                    "Differs from a fresh import"
                        + FirstDifference(path, import.Entries[language])
                        + ".",
                    path,
                    0,
                    0
                );
            }
        }

        foreach (
            var issue in LocalizationModelBuilder.Analyze(I18nPaths.ReadDataFiles(root)).Issues
        )
        {
            report.Add(issue);
        }

        var entryCount = import.Entries[recipe.Languages[0]].Count;
        output.WriteLine(
            report.Errors > 0
                ? string.Create(
                    CultureInfo.InvariantCulture,
                    $"i18n-import: {report.Errors} problems found. See the list above."
                )
            : check
                ? string.Create(
                    CultureInfo.InvariantCulture,
                    $"i18n-import: data/i18n matches a fresh import ({import.HandoffKeyCount} handoff keys, {entryCount} entries per language)."
                )
            : string.Create(
                CultureInfo.InvariantCulture,
                $"i18n-import: wrote {recipe.Languages.Length} strings files ({import.HandoffKeyCount} handoff keys, {entryCount} entries per language)."
            )
        );
        return report.Errors == 0 ? ExitCodes.Success : ExitCodes.Failure;
    }

    private static List<KeyValuePair<string, string>>? ReadFlat(string path, I18nReport report)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var entries = new List<KeyValuePair<string, string>>();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String)
                {
                    report.Add(
                        I18nCheckIds.ImportFailed,
                        "'" + property.Name + "' is not a string.",
                        path,
                        0,
                        0
                    );
                    return null;
                }

                entries.Add(new(property.Name, property.Value.GetString()!));
            }

            return entries;
        }
        catch (Exception ex) when (ex is JsonException or IOException or InvalidOperationException)
        {
            report.Add(
                I18nCheckIds.ImportFailed,
                "Cannot read the handoff file: " + ex.Message,
                path,
                0,
                0
            );
            return null;
        }
    }

    private static string FirstDifference(string path, List<KeyValuePair<string, string>> expected)
    {
        if (!File.Exists(path))
        {
            return " (the file does not exist)";
        }

        List<KeyValuePair<string, string>> actual;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            actual =
            [
                .. document
                    .RootElement.EnumerateObject()
                    .Select(static p => new KeyValuePair<string, string>(
                        p.Name,
                        p.Value.ToString()
                    )),
            ];
        }
        catch (JsonException)
        {
            return " (the file is not valid JSON)";
        }

        for (var i = 0; i < Math.Min(actual.Count, expected.Count); i++)
        {
            if (!string.Equals(actual[i].Key, expected[i].Key, StringComparison.Ordinal))
            {
                return " at entry "
                    + (i + 1).ToString(CultureInfo.InvariantCulture)
                    + ": '"
                    + actual[i].Key
                    + "' instead of '"
                    + expected[i].Key
                    + "'";
            }

            if (!string.Equals(actual[i].Value, expected[i].Value, StringComparison.Ordinal))
            {
                return " in the text of '" + expected[i].Key + "'";
            }
        }

        return actual.Count != expected.Count
            ? " in the number of entries ("
                + actual.Count.ToString(CultureInfo.InvariantCulture)
                + " instead of "
                + expected.Count.ToString(CultureInfo.InvariantCulture)
                + ")"
            : " in formatting only (run i18n-import to normalize it)";
    }
}
