using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Clicalo.Generators.Common;

namespace Clicalo.Generators.Localization;

/// <summary>
/// Reads and validates <c>data/i18n</c> (<c>locales.json</c>, <c>placeholders.json</c> and every
/// <c>strings.&lt;language&gt;.json</c>) and builds the model that <see cref="LocalizationEmitter"/> turns into code.
/// Pure and Roslyn-free: the generator and <c>cl i18n-check</c> run exactly this validation.
/// </summary>
internal sealed class LocalizationModelBuilder
{
    private static readonly ImmutableArray<string> LocaleProperties =
    [
        "code",
        "culture",
        "decimalSeparator",
        "nativeName",
        "plural",
        "shortName",
    ];

    private static readonly ImmutableArray<string> PlaceholderProperties = ["description", "type"];

    private readonly List<LocalizationIssue> _issues = [];
    private readonly HashSet<string> _brokenKeys = new(StringComparer.Ordinal);

    // Languages with a valid code in locales.json, even when another property of the entry is wrong: the entry's
    // own error is enough, and treating the language as undeclared would only add misleading errors.
    private readonly Dictionary<string, JsonNode> _declaredLanguages = new(StringComparer.Ordinal);

    private LocalizationModelBuilder() { }

    /// <summary>Analyses the given files; files whose name is not an i18n file are ignored.</summary>
    public static LocalizationModel Analyze(IEnumerable<LocalizationDataFile> files) =>
        new LocalizationModelBuilder().Build(files);

    private static string FileName(string path) => System.IO.Path.GetFileName(path);

    private static string Format(string format, params object[] args) =>
        string.Format(CultureInfo.InvariantCulture, format, args);

    private static string Braced(IEnumerable<string> names)
    {
        var list = names.Select(static n => "{" + n + "}").ToList();
        return list.Count == 0 ? "no placeholders" : string.Join(", ", list);
    }

    private LocalizationModel Build(IEnumerable<LocalizationDataFile> files)
    {
        var relevant = files
            .Where(static f => LocalizationFiles.IsRelevant(FileName(f.Path)))
            .OrderBy(static f => FileName(f.Path), StringComparer.OrdinalIgnoreCase)
            .ThenBy(static f => f.Path, StringComparer.Ordinal)
            .ToList();
        if (relevant.Count == 0)
        {
            return new LocalizationModel(null, [], [], [], []);
        }

        var localesFile = relevant.Find(static f =>
            string.Equals(
                FileName(f.Path),
                LocalizationFiles.Locales,
                StringComparison.OrdinalIgnoreCase
            )
        );
        var placeholdersFile = relevant.Find(static f =>
            string.Equals(
                FileName(f.Path),
                LocalizationFiles.Placeholders,
                StringComparison.OrdinalIgnoreCase
            )
        );

        var locales = ReadLocales(localesFile, out var defaultLanguage);
        var validLocales = locales.IsDefault ? [] : locales;
        var placeholders = ReadPlaceholders(placeholdersFile);
        var catalog = placeholders.IsDefault
            ? null
            : placeholders.ToDictionary(p => p.Name, StringComparer.Ordinal);

        var stringsFiles = relevant
            .Where(static f => LocalizationFiles.IsStringsFile(FileName(f.Path)))
            .ToList();
        var languages = new List<LanguageStrings>();
        foreach (var file in stringsFiles)
        {
            var strings = ReadStrings(file, catalog);
            if (strings is not null)
            {
                languages.Add(strings);
            }
        }

        MatchLocalesWithFiles(localesFile, locales, stringsFiles);
        foreach (var language in languages)
        {
            var locale = validLocales.FirstOrDefault(l =>
                string.Equals(l.Code, language.Code, StringComparison.Ordinal)
            );
            ValidateFamilies(language, locale, catalog);
        }

        var reference = languages.Find(l =>
            string.Equals(l.Code, defaultLanguage, StringComparison.Ordinal)
        );
        var messages = ImmutableArray<MessageDefinition>.Empty;
        if (reference is not null)
        {
            foreach (var other in languages.Where(l => !ReferenceEquals(l, reference)))
            {
                CompareLanguages(reference, other);
            }

            messages = BuildMessages(reference, catalog);
        }

        var issues = _issues
            .OrderBy(static i => i.Path ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static i => i.Line)
            .ThenBy(static i => i.Column)
            .ThenBy(static i => i.Id, StringComparer.Ordinal)
            .ThenBy(static i => i.Message, StringComparer.Ordinal)
            .ToImmutableArray();
        return new LocalizationModel(
            reference is null ? null : defaultLanguage,
            validLocales,
            placeholders.IsDefault ? [] : placeholders,
            messages,
            issues
        );
    }

    private void Report(string id, string message, string? path, int line, int column) =>
        _issues.Add(new LocalizationIssue(id, message, path, line, column));

    private void Report(string id, string message, string path, JsonNode node) =>
        Report(id, message, path, node.Line, node.Column);

    private JsonNode? Parse(LocalizationDataFile file, string missingId)
    {
        if (file.Text is null)
        {
            Report(
                missingId,
                Format("'{0}' could not be read.", FileName(file.Path)),
                file.Path,
                0,
                0
            );
            return null;
        }

        try
        {
            return MiniJson.Parse(file.Text);
        }
        catch (JsonParseException ex)
        {
            Report(
                LocalizationIds.InvalidJson,
                "Invalid JSON: " + ex.Message,
                file.Path,
                ex.Line,
                ex.Column
            );
            return null;
        }
    }

    private void RejectUnknownProperties(
        JsonNode node,
        ImmutableArray<string> allowed,
        string id,
        string path,
        JsonPositions positions,
        string owner
    )
    {
        foreach (var member in node.Members.Where(m => allowed.IndexOf(m.Key) < 0))
        {
            var (line, column) = positions.KeyOf(member.Value.Line, member.Value.Column);
            Report(
                id,
                Format(
                    "Unknown property '{0}' in {1}; expected {2}.",
                    member.Key,
                    owner,
                    string.Join(", ", allowed.Select(static a => "'" + a + "'"))
                ),
                path,
                line,
                column
            );
        }
    }

    private string? RequiredString(
        JsonNode owner,
        string property,
        string id,
        string path,
        string context
    )
    {
        var node = owner[property];
        if (node is { Kind: JsonKind.String } && !string.IsNullOrWhiteSpace(node.StringValue))
        {
            return node.StringValue;
        }

        Report(
            id,
            Format("{0} needs a non-empty string property '{1}'.", context, property),
            path,
            node ?? owner
        );
        return null;
    }

    private ImmutableArray<LocaleDefinition> ReadLocales(
        LocalizationDataFile? file,
        out string? defaultLanguage
    )
    {
        defaultLanguage = null;
        if (file is null)
        {
            Report(
                LocalizationIds.InvalidLocales,
                Format(
                    "'{0}/{1}' is missing.",
                    LocalizationFiles.Directory,
                    LocalizationFiles.Locales
                ),
                null,
                0,
                0
            );
            return default;
        }

        var root = Parse(file, LocalizationIds.InvalidLocales);
        if (root is null)
        {
            return default;
        }

        if (root.Kind != JsonKind.Object)
        {
            Report(
                LocalizationIds.InvalidLocales,
                "The root of locales.json must be an object with 'default' and 'locales'.",
                file.Path,
                root
            );
            return default;
        }

        var positions = new JsonPositions(file.Text!);
        RejectUnknownProperties(
            root,
            ["default", "locales"],
            LocalizationIds.InvalidLocales,
            file.Path,
            positions,
            "locales.json"
        );
        var declaredDefault = RequiredString(
            root,
            "default",
            LocalizationIds.InvalidLocales,
            file.Path,
            "locales.json"
        );
        var list = root["locales"];
        if (list is not { Kind: JsonKind.Array } || list.Items.Count == 0)
        {
            Report(
                LocalizationIds.InvalidLocales,
                "locales.json needs a non-empty array 'locales'.",
                file.Path,
                list ?? root
            );
            return default;
        }

        var locales = ImmutableArray.CreateBuilder<LocaleDefinition>();
        foreach (var entry in list.Items)
        {
            var locale = ReadLocale(entry, file.Path, positions);
            if (locale is not null)
            {
                locales.Add(locale);
            }
        }

        if (declaredDefault is not null)
        {
            if (_declaredLanguages.ContainsKey(declaredDefault))
            {
                defaultLanguage = declaredDefault;
            }
            else
            {
                Report(
                    LocalizationIds.InvalidLocales,
                    Format(
                        "The default language '{0}' is not declared in 'locales'.",
                        declaredDefault
                    ),
                    file.Path,
                    root["default"]!
                );
            }
        }

        return locales.ToImmutable();
    }

    private LocaleDefinition? ReadLocale(JsonNode entry, string path, JsonPositions positions)
    {
        if (entry.Kind != JsonKind.Object)
        {
            Report(
                LocalizationIds.InvalidLocales,
                "Each entry of 'locales' must be an object.",
                path,
                entry
            );
            return null;
        }

        RejectUnknownProperties(
            entry,
            LocaleProperties,
            LocalizationIds.InvalidLocales,
            path,
            positions,
            "a locale"
        );
        var code = RequiredString(entry, "code", LocalizationIds.InvalidLocales, path, "A locale");
        var culture = RequiredString(
            entry,
            "culture",
            LocalizationIds.InvalidLocales,
            path,
            "A locale"
        );
        var separator = RequiredString(
            entry,
            "decimalSeparator",
            LocalizationIds.InvalidLocales,
            path,
            "A locale"
        );
        _ = RequiredString(entry, "nativeName", LocalizationIds.InvalidLocales, path, "A locale");
        _ = RequiredString(entry, "shortName", LocalizationIds.InvalidLocales, path, "A locale");
        if (code is not null && !LocalizationFiles.IsValidLanguageCode(code))
        {
            Report(
                LocalizationIds.InvalidLocales,
                Format("'{0}' is not a language code such as 'es', 'en' or 'pt-BR'.", code),
                path,
                entry["code"]!
            );
            code = null;
        }
        else if (code is not null && _declaredLanguages.ContainsKey(code))
        {
            Report(
                LocalizationIds.InvalidLocales,
                Format("The language '{0}' is declared twice.", code),
                path,
                entry
            );
            return null;
        }
        else if (code is not null)
        {
            _declaredLanguages.Add(code, entry);
        }

        var categories = ReadPluralCategories(entry, path, positions);
        return code is null || culture is null || separator is null || categories.IsDefault
            ? null
            : new LocaleDefinition(code, culture, separator, categories, entry.Line, entry.Column);
    }

    private ImmutableArray<string> ReadPluralCategories(
        JsonNode entry,
        string path,
        JsonPositions positions
    )
    {
        var plural = entry["plural"];
        if (plural is not { Kind: JsonKind.Object })
        {
            Report(
                LocalizationIds.InvalidLocales,
                "A locale needs an object 'plural' with its CLDR rules (for example { \"one\": \"n = 1\" }).",
                path,
                plural ?? entry
            );
            return default;
        }

        var valid = true;
        var categories = new List<string> { PluralCategories.Other };
        foreach (var rule in plural.Members)
        {
            var (line, column) = positions.KeyOf(rule.Value.Line, rule.Value.Column);
            if (
                !PluralCategories.IsCategory(rule.Key)
                || string.Equals(rule.Key, PluralCategories.Other, StringComparison.Ordinal)
            )
            {
                Report(
                    LocalizationIds.InvalidLocales,
                    Format(
                        "'{0}' is not a plural category with a rule; use zero, one, two, few or many ('other' is implicit).",
                        rule.Key
                    ),
                    path,
                    line,
                    column
                );
                valid = false;
            }
            else if (
                rule.Value.Kind != JsonKind.String
                || string.IsNullOrWhiteSpace(rule.Value.StringValue)
            )
            {
                Report(
                    LocalizationIds.InvalidLocales,
                    Format(
                        "The plural rule '{0}' must be a non-empty CLDR condition string.",
                        rule.Key
                    ),
                    path,
                    rule.Value
                );
                valid = false;
            }
            else
            {
                categories.Add(rule.Key);
            }
        }

        return valid ? [.. categories.OrderBy(PluralCategories.OrderOf)] : default;
    }

    private ImmutableArray<PlaceholderDefinition> ReadPlaceholders(LocalizationDataFile? file)
    {
        if (file is null)
        {
            Report(
                LocalizationIds.InvalidPlaceholderCatalog,
                Format(
                    "'{0}/{1}' is missing.",
                    LocalizationFiles.Directory,
                    LocalizationFiles.Placeholders
                ),
                null,
                0,
                0
            );
            return default;
        }

        var root = Parse(file, LocalizationIds.InvalidPlaceholderCatalog);
        if (root is null)
        {
            return default;
        }

        if (root.Kind != JsonKind.Object)
        {
            Report(
                LocalizationIds.InvalidPlaceholderCatalog,
                "The root of placeholders.json must be an object of placeholder definitions.",
                file.Path,
                root
            );
            return default;
        }

        var positions = new JsonPositions(file.Text!);
        var result = ImmutableArray.CreateBuilder<PlaceholderDefinition>();
        var valid = true;
        foreach (var member in root.Members)
        {
            var definition = ReadPlaceholder(
                member.Key,
                member.Value,
                result.Count,
                file.Path,
                positions
            );
            if (definition is null)
            {
                valid = false;
            }
            else
            {
                result.Add(definition);
            }
        }

        return valid ? result.ToImmutable() : default;
    }

    private PlaceholderDefinition? ReadPlaceholder(
        string name,
        JsonNode value,
        int order,
        string path,
        JsonPositions positions
    )
    {
        if (!TemplateSyntax.IsValidName(name))
        {
            var (line, column) = positions.KeyOf(value.Line, value.Column);
            Report(
                LocalizationIds.InvalidPlaceholderCatalog,
                Format(
                    "'{0}' is not a valid placeholder name; use lower camelCase letters and digits.",
                    name
                ),
                path,
                line,
                column
            );
            return null;
        }

        if (value.Kind != JsonKind.Object)
        {
            Report(
                LocalizationIds.InvalidPlaceholderCatalog,
                Format(
                    "The placeholder '{0}' must be an object with 'type' and 'description'.",
                    name
                ),
                path,
                value
            );
            return null;
        }

        RejectUnknownProperties(
            value,
            PlaceholderProperties,
            LocalizationIds.InvalidPlaceholderCatalog,
            path,
            positions,
            "a placeholder"
        );
        var description = RequiredString(
            value,
            "description",
            LocalizationIds.InvalidPlaceholderCatalog,
            path,
            Format("The placeholder '{0}'", name)
        );
        var typeNode = value["type"];
        PlaceholderType? type = typeNode?.StringValue switch
        {
            "text" => PlaceholderType.Text,
            "integer" => PlaceholderType.Integer,
            "number" => PlaceholderType.Number,
            _ => null,
        };
        if (type is null)
        {
            Report(
                LocalizationIds.InvalidPlaceholderCatalog,
                Format(
                    "The placeholder '{0}' needs 'type': \"text\", \"integer\" or \"number\".",
                    name
                ),
                path,
                typeNode ?? value
            );
            return null;
        }

        return description is null
            ? null
            : new PlaceholderDefinition(name, type.Value, description, order);
    }

    private LanguageStrings? ReadStrings(
        LocalizationDataFile file,
        Dictionary<string, PlaceholderDefinition>? catalog
    )
    {
        var fileName = FileName(file.Path);
        var code = LocalizationFiles.LanguageOf(fileName);
        if (!LocalizationFiles.IsValidLanguageCode(code))
        {
            Report(
                LocalizationIds.InvalidLocales,
                Format(
                    "'{0}' must be named strings.<language>.json with a code such as 'es' or 'pt-BR'.",
                    fileName
                ),
                file.Path,
                1,
                1
            );
            return null;
        }

        var root = Parse(file, LocalizationIds.InvalidStringsFile);
        if (root is null)
        {
            return null;
        }

        if (root.Kind != JsonKind.Object)
        {
            Report(
                LocalizationIds.InvalidStringsFile,
                "A strings file must be a flat JSON object of key → text.",
                file.Path,
                root
            );
            return null;
        }

        var positions = new JsonPositions(file.Text!);
        var language = new LanguageStrings(code, file.Path);
        foreach (var member in root.Members)
        {
            var form = ReadForm(member.Key, member.Value, language, positions, catalog);
            if (form is not null)
            {
                language.Add(form);
            }
        }

        return language;
    }

    private StringForm? ReadForm(
        string key,
        JsonNode value,
        LanguageStrings language,
        JsonPositions positions,
        Dictionary<string, PlaceholderDefinition>? catalog
    )
    {
        var path = language.Path;
        var (keyLine, keyColumn) = positions.KeyOf(value.Line, value.Column);
        if (!KeyNaming.TrySplit(key, out var baseKey, out var category))
        {
            Report(
                LocalizationIds.InvalidKey,
                Format(
                    "Key '{0}' is not valid: use [A-Za-z][A-Za-z0-9]*, optionally followed by a plural suffix "
                        + "_zero, _one, _two, _few, _many or _other.",
                    key
                ),
                path,
                keyLine,
                keyColumn
            );
            return null;
        }

        if (value.Kind != JsonKind.String)
        {
            Report(
                LocalizationIds.InvalidStringsFile,
                Format(
                    "The value of '{0}' must be a string; nested objects and other values are not allowed.",
                    key
                ),
                path,
                value
            );
            _brokenKeys.Add(baseKey);
            return new StringForm(key, baseKey, category, string.Empty, [], keyLine, keyColumn);
        }

        var text = value.StringValue!;
        if (string.IsNullOrWhiteSpace(text))
        {
            Report(
                LocalizationIds.EmptyText,
                Format("The text of '{0}' is empty.", key),
                path,
                value
            );
        }

        if (!TemplateSyntax.TryParse(text, out var placeholders, out var error))
        {
            var (line, column) = positions.CharInString(value.Line, value.Column, error.Index);
            Report(
                LocalizationIds.MalformedPlaceholder,
                Format("Text of '{0}': {1}.", key, error.Message),
                path,
                line,
                column
            );
            _brokenKeys.Add(baseKey);
            return new StringForm(key, baseKey, category, text, [], keyLine, keyColumn);
        }

        if (catalog is not null)
        {
            foreach (var placeholder in placeholders.Where(p => !catalog.ContainsKey(p.Name)))
            {
                _brokenKeys.Add(baseKey);
                var (line, column) = positions.CharInString(
                    value.Line,
                    value.Column,
                    placeholder.Index
                );
                Report(
                    LocalizationIds.UnknownPlaceholder,
                    Format(
                        "Text of '{0}' uses the unknown placeholder {{{1}}}; declare it in placeholders.json or use one of {2}.",
                        key,
                        placeholder.Name,
                        Braced(catalog.Keys.OrderBy(static k => k, StringComparer.Ordinal))
                    ),
                    path,
                    line,
                    column
                );
            }
        }

        return new StringForm(key, baseKey, category, text, placeholders, keyLine, keyColumn);
    }

    private void MatchLocalesWithFiles(
        LocalizationDataFile? localesFile,
        ImmutableArray<LocaleDefinition> locales,
        List<LocalizationDataFile> stringsFiles
    )
    {
        if (localesFile is null || locales.IsDefault)
        {
            return;
        }

        // A strings file counts as present even when it is not valid JSON: that error is reported on its own.
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in stringsFiles)
        {
            var code = LocalizationFiles.LanguageOf(FileName(file.Path));
            if (LocalizationFiles.IsValidLanguageCode(code))
            {
                files[code] = file.Path;
            }
        }

        foreach (
            var declared in _declaredLanguages
                .Where(l => !files.ContainsKey(l.Key))
                .OrderBy(static l => l.Value.Line)
        )
        {
            Report(
                LocalizationIds.InvalidLocales,
                Format(
                    "The language '{0}' is declared but '{1}' does not exist.",
                    declared.Key,
                    LocalizationFiles.StringsFileName(declared.Key)
                ),
                localesFile.Path,
                declared.Value.Line,
                declared.Value.Column
            );
        }

        foreach (var file in files.OrderBy(static f => f.Key, StringComparer.Ordinal))
        {
            var code = file.Key;
            var path = file.Value;
            if (!_declaredLanguages.ContainsKey(code))
            {
                Report(
                    LocalizationIds.InvalidLocales,
                    Format(
                        "'{0}' has no entry in locales.json; declare the language '{1}' there.",
                        FileName(path),
                        code
                    ),
                    path,
                    1,
                    1
                );
            }
        }
    }

    private void ValidateFamilies(
        LanguageStrings language,
        LocaleDefinition? locale,
        Dictionary<string, PlaceholderDefinition>? catalog
    )
    {
        foreach (var family in language.Families)
        {
            if (family.HasPlain && family.HasPluralForms)
            {
                Report(
                    LocalizationIds.PluralShapeMismatch,
                    Format(
                        "'{0}' is both a plain text and a plural family in '{1}'; keep only the _category keys.",
                        family.BaseKey,
                        language.Code
                    ),
                    language.Path,
                    family.First.KeyLine,
                    family.First.KeyColumn
                );
                _brokenKeys.Add(family.BaseKey);
                continue;
            }

            if (family.HasPluralForms)
            {
                ValidatePluralFamily(language, family, locale, catalog);
            }
        }
    }

    private void ValidatePluralFamily(
        LanguageStrings language,
        StringFamily family,
        LocaleDefinition? locale,
        Dictionary<string, PlaceholderDefinition>? catalog
    )
    {
        var present = family.Categories.ToList();
        var required = locale is null ? [PluralCategories.Other] : locale.PluralCategories;
        foreach (var missing in required.Where(c => !present.Contains(c, StringComparer.Ordinal)))
        {
            Report(
                LocalizationIds.PluralFormMissing,
                Format(
                    "The plural family '{0}' in '{1}' lacks '{0}_{2}'{3}.",
                    family.BaseKey,
                    language.Code,
                    missing,
                    string.Equals(missing, PluralCategories.Other, StringComparison.Ordinal)
                        ? string.Empty
                        : ", required by the plural rules of the language"
                ),
                language.Path,
                family.First.KeyLine,
                family.First.KeyColumn
            );
        }

        if (locale is not null)
        {
            foreach (
                var form in family.Forms.Where(f =>
                    !locale.PluralCategories.Contains(f.Category!, StringComparer.Ordinal)
                )
            )
            {
                Report(
                    LocalizationIds.PluralFormUnexpected,
                    Format(
                        "'{0}' is not used: the plural rules of '{1}' only have {2}.",
                        form.Key,
                        language.Code,
                        string.Join(", ", locale.PluralCategories)
                    ),
                    language.Path,
                    form.KeyLine,
                    form.KeyColumn
                );
            }
        }

        var usesCount = family.PlaceholderNames.Contains(
            PluralCategories.SelectorArgument,
            StringComparer.Ordinal
        );
        PlaceholderDefinition? selector = null;
        var knownSelector =
            catalog is not null
            && catalog.TryGetValue(PluralCategories.SelectorArgument, out selector);
        if (!usesCount || (knownSelector && !selector!.IsNumeric))
        {
            Report(
                LocalizationIds.PluralWithoutCount,
                Format(
                    "The plural family '{0}' in '{1}' must use {{{2}}}, an integer or number placeholder, to select its form.",
                    family.BaseKey,
                    language.Code,
                    PluralCategories.SelectorArgument
                ),
                language.Path,
                family.First.KeyLine,
                family.First.KeyColumn
            );
        }
    }

    private void CompareLanguages(LanguageStrings reference, LanguageStrings other)
    {
        foreach (var family in reference.Families.Where(f => !other.TryGet(f.BaseKey, out _)))
        {
            ReportMissing(family, reference, other);
        }

        foreach (var family in other.Families)
        {
            if (!reference.TryGet(family.BaseKey, out var expected))
            {
                ReportMissing(family, other, reference);
                continue;
            }

            if (_brokenKeys.Contains(family.BaseKey))
            {
                continue;
            }

            if (expected.HasPluralForms != family.HasPluralForms)
            {
                Report(
                    LocalizationIds.PluralShapeMismatch,
                    Format(
                        "'{0}' is {1} in '{2}' but {3} in '{4}'.",
                        family.BaseKey,
                        expected.HasPluralForms ? "a plural family" : "a plain text",
                        reference.Code,
                        family.HasPluralForms ? "a plural family" : "a plain text",
                        other.Code
                    ),
                    other.Path,
                    family.First.KeyLine,
                    family.First.KeyColumn
                );
                continue;
            }

            var expectedNames = expected.PlaceholderNames;
            var actualNames = family.PlaceholderNames;
            if (!expectedNames.SequenceEqual(actualNames, StringComparer.Ordinal))
            {
                Report(
                    LocalizationIds.PlaceholderMismatch,
                    Format(
                        "'{0}' uses {1} in '{2}' but {3} in '{4}'.",
                        family.BaseKey,
                        Braced(expectedNames),
                        reference.Code,
                        Braced(actualNames),
                        other.Code
                    ),
                    other.Path,
                    family.First.KeyLine,
                    family.First.KeyColumn
                );
            }
        }
    }

    private void ReportMissing(
        StringFamily family,
        LanguageStrings present,
        LanguageStrings absent
    ) =>
        Report(
            LocalizationIds.MissingTranslation,
            Format(
                "'{0}' exists in '{1}' but is missing in '{2}' ({3}).",
                family.BaseKey,
                present.Code,
                absent.Code,
                FileName(absent.Path)
            ),
            present.Path,
            family.First.KeyLine,
            family.First.KeyColumn
        );

    private ImmutableArray<MessageDefinition> BuildMessages(
        LanguageStrings reference,
        Dictionary<string, PlaceholderDefinition>? catalog
    )
    {
        var members = new Dictionary<string, StringFamily>(StringComparer.Ordinal);
        var messages = ImmutableArray.CreateBuilder<MessageDefinition>();
        foreach (var family in reference.Families)
        {
            var member = KeyNaming.ToMemberName(family.BaseKey);
            if (KeyNaming.IsReserved(member))
            {
                Report(
                    LocalizationIds.MemberNameCollision,
                    Format(
                        "'{0}' would generate the member '{1}.{2}', which is reserved; rename the key.",
                        family.BaseKey,
                        KeyNaming.MessagesClass,
                        member
                    ),
                    reference.Path,
                    family.First.KeyLine,
                    family.First.KeyColumn
                );
                continue;
            }

            if (members.TryGetValue(member, out var previous))
            {
                Report(
                    LocalizationIds.MemberNameCollision,
                    Format(
                        "'{0}' and '{1}' would both generate the member '{2}.{3}'; rename one of them.",
                        previous.BaseKey,
                        family.BaseKey,
                        KeyNaming.MessagesClass,
                        member
                    ),
                    reference.Path,
                    family.First.KeyLine,
                    family.First.KeyColumn
                );
                continue;
            }

            members.Add(member, family);
            if (family.HasPlain && family.HasPluralForms)
            {
                continue;
            }

            var arguments = family
                .PlaceholderNames.Select(n =>
                    catalog is not null && catalog.TryGetValue(n, out var definition)
                        ? definition
                        : null
                )
                .Where(static d => d is not null)
                .Select(static d => d!)
                .OrderBy(static d => d.Order)
                .ToImmutableArray();
            var forms = family
                .Forms.OrderBy(static f =>
                    f.Category is null ? -1 : PluralCategories.OrderOf(f.Category)
                )
                .Select(static f => new MessageForm(f.Category, f.Text))
                .ToImmutableArray();
            messages.Add(
                new MessageDefinition(
                    family.BaseKey,
                    member,
                    family.HasPluralForms,
                    arguments,
                    forms
                )
            );
        }

        return messages.ToImmutable();
    }
}
