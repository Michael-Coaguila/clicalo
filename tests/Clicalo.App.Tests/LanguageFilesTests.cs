using System.IO;
using System.Text.Json;
using Clicalo.App.Localization;
using Clicalo.TestKit;

namespace Clicalo.App.Tests;

/// <summary>
/// The language files next to the executable (blueprint §8.5, ADR-0011): a broken language is left out and the others
/// still start; only a broken <c>locales.json</c> or no texts for the default language stop the start.
/// </summary>
[Trait("Req", "IDI-001")]
public sealed class LanguageFilesTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "clicalo-app-tests",
        Guid.NewGuid().ToString("N"),
        "i18n"
    );

    public LanguageFilesTests()
    {
        Directory.CreateDirectory(_folder);
        foreach (var file in new[] { "locales.json", "strings.es.json", "strings.en.json" })
        {
            File.Copy(RepoPaths.Combine("data", "i18n", file), Path.Combine(_folder, file));
        }
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path.GetDirectoryName(_folder)!, recursive: true);
        }
        catch (IOException)
        {
            // Cleaned by the system.
        }
    }

    [Fact]
    public void The_shipped_files_load_every_language()
    {
        var context = LanguageFiles.Load(_folder, "en", out var skipped);

        skipped.ShouldBeEmpty();
        context.Languages.Select(l => l.Code).ShouldBe(["es", "en"]);
        context.Current.Locale.Code.ShouldBe("en");
    }

    [Fact]
    public void A_corrupt_language_is_left_out_and_the_default_starts()
    {
        Replace("strings.en.json", "{ \"broken\": ");

        var context = LanguageFiles.Load(_folder, "en", out var skipped);

        skipped.ShouldBe(["en"]);
        context.Languages.Select(l => l.Code).ShouldBe(["es"]);
        context.Current.Locale.Code.ShouldBe("es");
    }

    [Fact]
    public void A_broken_entry_of_locales_json_is_left_out()
    {
        Replace(
            "locales.json",
            File.ReadAllText(Path.Combine(_folder, "locales.json"))
                .Replace("\"one\": \"i = 1", "\"several\": \"i = 1", StringComparison.Ordinal)
        );

        _ = LanguageFiles.Load(_folder, null, out var skipped);

        skipped.ShouldBe(["en"]);
    }

    [Fact]
    public void Without_the_texts_of_the_default_language_the_start_stops()
    {
        Replace("strings.es.json", "not json");

        Should.Throw<ArgumentException>(() => LanguageFiles.Load(_folder, null, out _));
    }

    [Fact]
    public void A_corrupt_locales_json_stops_the_start()
    {
        Replace("locales.json", "{");

        Should
            .Throw<Exception>(() => LanguageFiles.Load(_folder, null, out _))
            .ShouldBeAssignableTo<JsonException>();
    }

    private void Replace(string file, string text) =>
        File.WriteAllText(Path.Combine(_folder, file), text);
}
