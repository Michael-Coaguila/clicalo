using System.Globalization;
using Clicalo.Application.UseCases;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.Domain.Templates;
using Clicalo.Infrastructure.Catalogs;
using Clicalo.TestKit;

namespace Clicalo.Infrastructure.Tests.Catalogs;

/// <summary>
/// The shipped starter content of <c>data/content</c>, read as the app reads it and turned into first documents (user
/// decision D2 of 2026-10-03): «Basics» by default, the nine templates on demand, each bound to all its programs.
/// </summary>
[Trait("Req", "CAT-003")]
[Trait("Req", "BIE-006")]
public sealed class StarterContentFilesTests : IDisposable
{
    private static readonly string Shipped = Path.Combine(RepoPaths.Data, "content");

    private static readonly Lazy<StarterContent> Content = new(() =>
        StarterContentFiles.Load(Shipped).ShouldNotBeNull()
    );

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "clicalo-starter-tests",
        Guid.NewGuid().ToString("N")
    );

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
            // Nothing was copied.
        }
    }

    [Fact]
    public void The_shipped_kit_offers_basics_and_the_nine_templates_in_order()
    {
        var content = Content.Value;

        StarterContentFiles.Exists(Shipped).ShouldBeTrue();
        content
            .Kit.Options.Select(o => o.Id)
            .ShouldBe([
                "basics",
                "word",
                "browser",
                "vscode",
                "excel",
                "ppt",
                "zoom",
                "explorer",
                "outlook",
                "notepad",
            ]);
        content.Templates.Select(t => t.Id).ShouldBe(content.Kit.Options.Skip(1).Select(o => o.Id));
        content.Kit.DefaultSelection.ShouldBe(StarterSelection.Of(["basics"]));
        content.Seed.AlwaysVisible.Count.ShouldBe(4);
        content.Seed.General.Count.ShouldBe(12);
        foreach (var template in content.Templates)
        {
            var file = Path.Combine(Shipped, "templates", template.Id + ".json");
            template.Shortcuts.Count.ShouldBe(
                CountShortcuts(file),
                template.Id + ": no shipped shortcut may be left out by the reader"
            );
        }
    }

    [Fact]
    [Trait("Req", "BIE-003")]
    public void The_default_first_document_is_basics_only()
    {
        var document = FirstDocument
            .CreateDefault(Content.Value, SettingsSchema.Defaults, new Ids())
            .Value;

        document.Validate().ShouldBeEmpty();
        var library = document.Library;
        library.AlwaysVisible.Select(Item).ShouldBe(["dict", "ptt", "mute", "desk"]);
        library.Profiles.ShouldHaveSingleItem().Id.ShouldBe(ProfileId.General);
        library.General.Shortcuts.Count.ShouldBe(12);
        library
            .General.Shortcuts.Single(s => string.Equals(Item(s), "copy", StringComparison.Ordinal))
            .Origin.ShouldBe(new CatalogRef(SeedContent.Source, "1", "copy"));
    }

    [Fact]
    [Trait("Req", "PER-002")]
    [Trait("Req", "PLA-013")]
    [Trait("Req", "DAT-005")]
    public void Every_template_marked_binds_all_its_programs_without_sharing_one()
    {
        var all = StarterSelection.Of(Content.Value.Kit.Options.Select(o => o.Id));

        var document = FirstDocument
            .Create(Content.Value, all, SettingsSchema.Defaults, new Ids())
            .Value;

        document.Validate().ShouldBeEmpty();
        var library = document.Library;
        library.Profiles.Count.ShouldBe(10);
        foreach (
            var browser in new[]
            {
                "chrome.exe",
                "MSEDGE.EXE",
                "firefox.exe",
                "brave.exe",
                "opera.exe",
            }
        )
        {
            library
                .ProfileFor(new ProcessName(browser))!
                .Origin!.Value.Source.ShouldBe("browser", browser);
        }

        library
            .ProfileFor(new ProcessName("OUTLOOK.EXE"))!
            .Origin!.Value.Source.ShouldBe("outlook");
        library.ProfileFor(new ProcessName("olk.exe"))!.Origin!.Value.Source.ShouldBe("outlook");
        library
            .ProfileFor(new ProcessName("Zoom.exe"))!
            .Name.Get(LangCode.Es, LangCode.En)
            .ShouldBe("Zoom");
    }

    [Theory]
    [Trait("Req", "CAT-005")]
    [InlineData("es", KeyName.N)]
    [InlineData("en", KeyName.B)]
    public void Word_bold_follows_the_programs_language(string appsLanguage, KeyName expected)
    {
        var settings = SettingsSchema.Defaults with
        {
            Keyboard = SettingsSchema.Defaults.Keyboard with
            {
                AppsLanguage = new LangCode(appsLanguage),
            },
        };

        var library = FirstDocument
            .Create(Content.Value, StarterSelection.Of(["word"]), settings, new Ids())
            .Value.Library;

        var bold = library
            .ProfileFor(new ProcessName("winword.exe"))!
            .Shortcuts.Single(s => string.Equals(Item(s), "bold", StringComparison.Ordinal));
        ((TapAction)bold.Action).Chord.ShouldBe(
            KeyChord.FromKeys(KeyIds.Ctrl, expected == KeyName.N ? KeyIds.N : KeyIds.B)
        );
    }

    [Fact]
    [Trait("Req", "LOG-006")]
    public void A_template_that_cannot_be_read_is_not_offered()
    {
        var copy = CopyShipped();
        File.WriteAllText(Path.Combine(copy, "templates", "zoom.json"), "{ broken");
        File.Delete(Path.Combine(copy, "templates", "notepad.json"));

        var content = StarterContentFiles.Load(copy).ShouldNotBeNull();

        content.Kit.Options.Select(o => o.Id).ShouldNotContain("zoom", StringComparer.Ordinal);
        content.Kit.Options.Select(o => o.Id).ShouldNotContain("notepad", StringComparer.Ordinal);
        content.Templates.Count.ShouldBe(7);
    }

    [Theory]
    [Trait("Req", "LOG-006")]
    [InlineData("starter.json")]
    [InlineData("seed.json")]
    public void Without_a_readable_kit_or_seed_there_is_no_content(string file)
    {
        var copy = CopyShipped();
        File.WriteAllText(Path.Combine(copy, file), "[]");

        StarterContentFiles.Load(copy).ShouldBeNull();
        StarterContentFiles.Load(Path.Combine(_root, "missing")).ShouldBeNull();
    }

    /// <summary>Which key the expected Bold ends with (xUnit data must be serializable).</summary>
    public enum KeyName
    {
        /// <summary>Ctrl+N, Bold of Spanish Office.</summary>
        N,

        /// <summary>Ctrl+B, Bold of English Office.</summary>
        B,
    }

    private string CopyShipped()
    {
        var copy = Path.Combine(_root, "content");
        Directory.CreateDirectory(Path.Combine(copy, "templates"));
        foreach (var file in Directory.GetFiles(Shipped, "*.json"))
        {
            File.Copy(file, Path.Combine(copy, Path.GetFileName(file)));
        }

        foreach (var file in Directory.GetFiles(Path.Combine(Shipped, "templates"), "*.json"))
        {
            File.Copy(file, Path.Combine(copy, "templates", Path.GetFileName(file)));
        }

        return copy;
    }

    private static int CountShortcuts(string file)
    {
        using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(file));
        return json.RootElement.GetProperty("shortcuts").GetArrayLength();
    }

    private static string Item(Shortcut shortcut) => shortcut.Origin!.Value.ItemId;

    private sealed class Ids : IIdGenerator
    {
        private int _next;

        public ProfileId NewProfileId() => new("p" + Next());

        public ShortcutId NewShortcutId() => new("s" + Next());

        private string Next() => (++_next).ToString(CultureInfo.InvariantCulture);
    }
}
