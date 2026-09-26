using System;

namespace Clicalo.Generators.Localization;

/// <summary>Names of the files of <c>data/i18n</c> that take part in validation and code generation.</summary>
internal static class LocalizationFiles
{
    /// <summary>Repository-relative folder of the i18n data.</summary>
    public const string Directory = "data/i18n";

    /// <summary>Declares the languages, the default one, number format and CLDR plural rules.</summary>
    public const string Locales = "locales.json";

    /// <summary>Closed vocabulary of named placeholders with their type.</summary>
    public const string Placeholders = "placeholders.json";

    private const string StringsPrefix = "strings.";
    private const string JsonExtension = ".json";

    /// <summary>File name of the strings of a language, for example <c>strings.es.json</c>.</summary>
    public static string StringsFileName(string languageCode) =>
        StringsPrefix + languageCode + JsonExtension;

    /// <summary>True when the file takes part in the i18n analysis (other files in the folder are ignored).</summary>
    public static bool IsRelevant(string fileName) =>
        string.Equals(fileName, Locales, StringComparison.OrdinalIgnoreCase)
        || string.Equals(fileName, Placeholders, StringComparison.OrdinalIgnoreCase)
        || IsStringsFile(fileName);

    /// <summary>True for <c>strings.*.json</c>, whether or not its language code is valid.</summary>
    public static bool IsStringsFile(string fileName) =>
        fileName.StartsWith(StringsPrefix, StringComparison.OrdinalIgnoreCase)
        && fileName.EndsWith(JsonExtension, StringComparison.OrdinalIgnoreCase)
        && fileName.Length > StringsPrefix.Length + JsonExtension.Length;

    /// <summary>Extracts the language code of a strings file name (<c>strings.es.json</c> → <c>es</c>).</summary>
    public static string LanguageOf(string stringsFileName) =>
        stringsFileName.Substring(
            StringsPrefix.Length,
            stringsFileName.Length - StringsPrefix.Length - JsonExtension.Length
        );

    /// <summary>
    /// A BCP 47-like language code: a 2–3 letter lowercase language, optionally followed by subtags
    /// (<c>es</c>, <c>en</c>, <c>pt-BR</c>).
    /// </summary>
    public static bool IsValidLanguageCode(string code)
    {
        var parts = code.Split('-');
        if (parts[0].Length is < 2 or > 3)
        {
            return false;
        }

        foreach (var c in parts[0])
        {
            if (c is < 'a' or > 'z')
            {
                return false;
            }
        }

        for (var i = 1; i < parts.Length; i++)
        {
            if (parts[i].Length is < 2 or > 8)
            {
                return false;
            }

            foreach (var c in parts[i])
            {
                if (!(c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9'))
                {
                    return false;
                }
            }
        }

        return true;
    }
}
