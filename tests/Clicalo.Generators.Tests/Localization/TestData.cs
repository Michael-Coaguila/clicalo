namespace Clicalo.Generators.Tests.Localization;

/// <summary>A small, valid <c>data/i18n</c> with every shape of entry; tests break one thing at a time.</summary>
internal static class TestData
{
    public const string Locales = """
        {
          "default": "es",
          "locales": [
            { "code": "es", "culture": "es", "nativeName": "Español", "shortName": "ES", "decimalSeparator": ",", "plural": { "one": "n = 1" } },
            { "code": "en", "culture": "en", "nativeName": "English", "shortName": "EN", "decimalSeparator": ".", "plural": { "one": "i = 1 and v = 0" } }
          ]
        }
        """;

    public const string Placeholders = """
        {
          "app": { "type": "text", "description": "Nombre de la app." },
          "profiles": { "type": "text", "description": "Frase con los perfiles." },
          "count": { "type": "integer", "description": "Cantidad." },
          "ratio": { "type": "number", "description": "Proporción." }
        }
        """;

    // Line 2 search, 3 comboN_one, 4 comboN_other, 5 zoom, 6 createFor, 7 impT.
    public const string Es = """
        {
          "search": "Buscar",
          "comboN_one": "{count} tecla",
          "comboN_other": "{count} teclas",
          "zoom": "Zoom {ratio}",
          "createFor": "Crear para {app}",
          "impT": "Importado: {profiles} & <más>"
        }
        """;

    public const string En = """
        {
          "search": "Search",
          "comboN_one": "{count} key",
          "comboN_other": "{count} keys",
          "zoom": "Zoom {ratio}",
          "createFor": "Create for {app}",
          "impT": "Imported: {profiles} & <more>"
        }
        """;

    public static Dictionary<string, string> Valid() =>
        new(StringComparer.Ordinal)
        {
            ["locales.json"] = Locales,
            ["placeholders.json"] = Placeholders,
            ["strings.es.json"] = Es,
            ["strings.en.json"] = En,
        };

    /// <summary>The valid data with one file replaced (or removed when <paramref name="content"/> is null).</summary>
    public static Dictionary<string, string> With(string file, string? content)
    {
        var files = Valid();
        if (content is null)
        {
            files.Remove(file);
        }
        else
        {
            files[file] = content;
        }

        return files;
    }

    /// <summary>The valid data with a line inserted before the closing brace of a strings file.</summary>
    public static Dictionary<string, string> WithEntry(string file, string entry)
    {
        var files = Valid();
        var text = files[file];
        var close = text.LastIndexOf('}');
        files[file] = text[..close].TrimEnd() + ",\n  " + entry + "\n}";
        return files;
    }

    /// <summary>The valid data with one exact text fragment replaced in a file.</summary>
    public static Dictionary<string, string> Replace(string file, string oldValue, string newValue)
    {
        var files = Valid();
        files[file].ShouldContain(oldValue);
        files[file] = files[file].Replace(oldValue, newValue, StringComparison.Ordinal);
        return files;
    }
}
