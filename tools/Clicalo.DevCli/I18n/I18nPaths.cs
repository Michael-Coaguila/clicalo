using Clicalo.Generators.Localization;

namespace Clicalo.DevCli.I18n;

/// <summary>Locations of the i18n data inside the repository.</summary>
internal static class I18nPaths
{
    /// <summary>Keys that may stay unused by the code; see <c>docs/guides/i18n.md</c>.</summary>
    public const string AllowUnusedFileName = "allow-unused.txt";

    /// <summary>The reviewed conversion recipe from the design handoff.</summary>
    public const string RecipeFileName = "handoff-import.json";

    public static string Directory(string root) => Path.Combine(root, "data", "i18n");

    public static string AllowUnused(string root) =>
        Path.Combine(Directory(root), AllowUnusedFileName);

    public static string Recipe(string root) => Path.Combine(Directory(root), RecipeFileName);

    public static string Strings(string root, string language) =>
        Path.Combine(Directory(root), LocalizationFiles.StringsFileName(language));

    /// <summary>Every file of <c>data/i18n</c>; the analysis itself ignores the ones that are not i18n data.</summary>
    public static List<LocalizationDataFile> ReadDataFiles(string root)
    {
        var directory = Directory(root);
        if (!System.IO.Directory.Exists(directory))
        {
            return [];
        }

        return
        [
            .. System
                .IO.Directory.EnumerateFiles(directory)
                .Order(StringComparer.Ordinal)
                .Select(static path => new LocalizationDataFile(path, File.ReadAllText(path))),
        ];
    }
}
