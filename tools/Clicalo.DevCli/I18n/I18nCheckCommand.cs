using System.Globalization;
using Clicalo.Application.Localization;
using Clicalo.Generators.Common;
using Clicalo.Generators.Localization;

namespace Clicalo.DevCli.I18n;

/// <summary>
/// <c>cl i18n-check</c>: the generator's validation of <c>data/i18n</c> (the same code, CLCI001–015), the syntax of
/// the CLDR plural rules (the runtime parser) and <c>allow-unused.txt</c> against the product code (CLCI101–104).
/// </summary>
internal static class I18nCheckCommand
{
    public static int Run(string root, bool strictUnused, TextWriter output)
    {
        var report = new I18nReport(root, output);
        var files = I18nPaths.ReadDataFiles(root);
        var model = LocalizationModelBuilder.Analyze(files);
        foreach (var issue in model.Issues)
        {
            report.Add(issue);
        }

        CheckPluralRules(files, report);
        var allowed = 0;
        var unused = 0;
        if (!model.Messages.IsEmpty)
        {
            (allowed, unused) = CheckUsage(root, model, strictUnused, report);
        }

        var summary =
            report.Errors == 0
                ? string.Create(
                    CultureInfo.InvariantCulture,
                    $"i18n-check: no problems. {model.Messages.Length} keys in {model.Locales.Length} languages; {allowed} allowed unused; {unused} unused and not allowed."
                )
                : string.Create(
                    CultureInfo.InvariantCulture,
                    $"i18n-check: {report.Errors} problems found. See the list above."
                );
        output.WriteLine(summary);
        return report.Errors == 0 ? ExitCodes.Success : ExitCodes.Failure;
    }

    private static void CheckPluralRules(List<LocalizationDataFile> files, I18nReport report)
    {
        var locales = files.Find(static f =>
            string.Equals(
                Path.GetFileName(f.Path),
                LocalizationFiles.Locales,
                StringComparison.OrdinalIgnoreCase
            )
        );
        JsonNode root;
        try
        {
            root = MiniJson.Parse(locales?.Text ?? string.Empty);
        }
        catch (JsonParseException)
        {
            return; // Already reported as CLCI001 or CLCI013.
        }

        foreach (var locale in root["locales"]?.Items ?? [])
        {
            foreach (var rule in locale["plural"]?.Members ?? [])
            {
                if (rule.Value.StringValue is not { } condition)
                {
                    continue;
                }

                try
                {
                    _ = PluralRule.Parse(condition);
                }
                catch (FormatException ex)
                {
                    report.Add(
                        LocalizationIds.InvalidLocales,
                        ex.Message,
                        locales!.Path,
                        rule.Value.Line,
                        rule.Value.Column
                    );
                }
            }
        }
    }

    private static (int Allowed, int Unused) CheckUsage(
        string root,
        LocalizationModel model,
        bool strictUnused,
        I18nReport report
    )
    {
        var keys = model.Messages.Select(static m => m.Key).ToHashSet(StringComparer.Ordinal);
        var memberToKey = model.Messages.ToDictionary(
            static m => m.MemberName,
            static m => m.Key,
            StringComparer.Ordinal
        );
        var usages = KeyUsageScanner.Scan(root, memberToKey);
        var path = I18nPaths.AllowUnused(root);
        if (!File.Exists(path))
        {
            report.Add(
                I18nCheckIds.AllowUnusedUnknown,
                I18nPaths.AllowUnusedFileName + " is missing.",
                null,
                0,
                0
            );
            return (0, 0);
        }

        var allowed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in AllowUnusedList.Parse(File.ReadAllText(path)))
        {
            if (!keys.Contains(entry.Key))
            {
                report.Add(
                    I18nCheckIds.AllowUnusedUnknown,
                    "'"
                        + entry.Key
                        + "' is not a key of data/i18n (use the base key, without plural suffix).",
                    path,
                    entry.Line,
                    1
                );
            }
            else if (!allowed.Add(entry.Key))
            {
                report.Add(
                    I18nCheckIds.AllowUnusedRepeated,
                    "'" + entry.Key + "' is listed twice.",
                    path,
                    entry.Line,
                    1
                );
            }
            else if (usages.TryGetValue(entry.Key, out var usage))
            {
                report.Add(
                    I18nCheckIds.AllowUnusedButUsed,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"'{entry.Key}' is used in {RepositoryRoot.Relative(root, usage.Path)}({usage.Line},{usage.Column}); remove it from {I18nPaths.AllowUnusedFileName}."
                    ),
                    path,
                    entry.Line,
                    1
                );
            }
        }

        var unused = model
            .Messages.Where(m => !usages.ContainsKey(m.Key) && !allowed.Contains(m.Key))
            .ToList();
        if (strictUnused)
        {
            foreach (var message in unused)
            {
                report.Add(
                    I18nCheckIds.UnusedKey,
                    "'"
                        + message.Key
                        + "' is not used by the product and is not in "
                        + I18nPaths.AllowUnusedFileName
                        + ".",
                    null,
                    0,
                    0
                );
            }
        }

        return (allowed.Count, unused.Count);
    }
}
