using System.Text;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Templates;
using Clicalo.Infrastructure.Catalogs;

namespace Clicalo.Infrastructure.Tests.Catalogs;

/// <summary>
/// The starter content is untrusted (LOG-006): each file is validated again when it is read, a file that does not
/// validate gives nothing and a shortcut that does not validate is left out rather than guessed.
/// </summary>
[Trait("Req", "LOG-006")]
public sealed class StarterContentReaderTests
{
    private const string Seed = """
        {"catalogVersion":2,
         "alwaysVisible":[{"id":"a","name":{"es":"A","en":"A"},"icon":"mic","category":"voice","action":{"type":"tap","keys":["win","h"]}}],
         "general":{"name":{"es":"General","en":"General"},"icon":"apps","shortcuts":[
           {"id":"h","name":{"es":"H","en":"H"},"icon":"bolt","category":"edit","action":{"type":"hold","keys":["ctrl","space"]}},
           {"id":"t","name":{"es":"T","en":"T"},"icon":"bolt","category":"edit","action":{"type":"toggle","keys":["shift"]}},
           {"id":"m","name":{"es":"M","en":"M"},"icon":"bolt","category":"nav","action":{"type":"mouse","mouse":"drag"}},
           {"id":"c","name":{"es":"C","en":"C"},"icon":"close","category":"win","action":{"type":"tap","keys":["alt","f4"]},"confirm":true}]}}
        """;

    [Fact]
    [Trait("Req", "CAT-003")]
    public void A_seed_keeps_every_kind_and_its_meaning()
    {
        var seed = StarterContentReader.ReadSeed(Bytes(Seed)).ShouldNotBeNull();

        seed.CatalogVersion.ShouldBe(2);
        seed.AlwaysVisible.ShouldHaveSingleItem().ItemId.ShouldBe("a");
        seed.GeneralName.Get(LangCode.Es, LangCode.En).ShouldBe("General");
        seed.General.Select(s => s.Action)
            .ShouldBe([
                new HoldAction(KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.Space)),
                new ToggleAction(KeyChord.FromKeys(KeyIds.Shift)),
                new MouseAction(MouseOp.Drag, ScrollSpeed.Normal),
                new TapAction(KeyChord.FromKeys(KeyIds.Alt, KeyIds.F4), []),
            ]);
        seed.General[^1].Confirm.ShouldBeTrue("Alt+F4 asks for a second tap (EJE-002)");
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("""{"catalogVersion":1}""")]
    [InlineData(
        """{"catalogVersion":0,"alwaysVisible":[],"general":{"name":{"es":"G"},"icon":"apps","shortcuts":[]}}"""
    )]
    [InlineData(
        """{"catalogVersion":1,"alwaysVisible":[],"general":{"name":{"es":" "},"icon":"apps","shortcuts":[]}}"""
    )]
    [InlineData(
        """{"catalogVersion":1,"alwaysVisible":[],"general":{"name":{"es":"G"},"icon":"apps","shortcuts":[{"id":"a"}]}}"""
    )]
    [InlineData(
        """{"catalogVersion":1,/* no */"alwaysVisible":[],"general":{"name":{"es":"G"},"icon":"apps","shortcuts":[]}}"""
    )]
    public void A_broken_seed_gives_nothing(string json) =>
        StarterContentReader.ReadSeed(Bytes(json)).ShouldBeNull();

    [Theory]
    [InlineData(
        """{"type":"tap","keys":["ctrl","nokey"]}""",
        "a key outside the catalog (CAT-004)"
    )]
    [InlineData("""{"type":"tap","keys":[]}""", "an empty combination")]
    [InlineData(
        """{"type":"tap","keys":["ctrl","n"],"variants":{"en":["ctrl","nokey"]}}""",
        "a variant outside the catalog"
    )]
    [InlineData(
        """{"type":"web","url":"file:///c:/windows"}""",
        "an address that is not http or https (EJE-011)"
    )]
    [InlineData(
        """{"type":"macro","steps":[{"kind":"wait","ms":1}]}""",
        "a wait out of range (I6)"
    )]
    [InlineData("""{"type":"macro","steps":[]}""", "a macro without steps")]
    [InlineData(
        """{"type":"app","target":"cmd.exe"}""",
        "an app, which content never ships (LOG-008)"
    )]
    [InlineData("""{"type":"text","text":"x"}""", "a text, which only the library offers")]
    [InlineData("""{"type":"mouse","mouse":"fly"}""", "an unknown mouse action")]
    public void A_shortcut_that_does_not_validate_is_left_out(string action, string because)
    {
        var json = $$$"""
            {"catalogVersion":1,"alwaysVisible":[],"general":{"name":{"es":"G","en":"G"},"icon":"apps","shortcuts":[
              {"id":"bad","name":{"es":"B","en":"B"},"icon":"bolt","category":"edit","action":{{{action}}}},
              {"id":"ok","name":{"es":"O","en":"O"},"icon":"bolt","category":"edit","action":{"type":"tap","keys":["ctrl","c"]}}]}}
            """;

        StarterContentReader
            .ReadSeed(Bytes(json))
            .ShouldNotBeNull()
            .General.ShouldHaveSingleItem(because)
            .ItemId.ShouldBe("ok");
    }

    [Fact]
    [Trait("Req", "CAT-005")]
    [Trait("Req", "CAT-006")]
    public void A_template_keeps_its_processes_variants_and_macro()
    {
        const string json = """
            {"id":"word","version":3,"authors":["x"],"appsLanguages":["es","en"],"name":{"es":"Word","en":"Word"},"icon":"description",
             "processes":["winword.exe","WINWORD32.exe"],
             "shortcuts":[
               {"id":"bold","name":{"es":"Negrita","en":"Bold"},"icon":"format_bold","category":"fmt","action":{"type":"tap","keys":["ctrl","n"],"variants":{"en":["ctrl","b"]}}},
               {"id":"pdf","name":{"es":"PDF","en":"PDF"},"icon":"picture_as_pdf","category":"file","action":{"type":"macro","steps":[{"kind":"keys","keys":["f12"]},{"kind":"wait","ms":500},{"kind":"text","text":"PDF"},{"kind":"mouse","mouse":"dbl"}]}},
               {"id":"mail","name":{"es":"Correo","en":"Mail"},"icon":"mail","category":"web","action":{"type":"web","url":"https://mail.example.org"}}]}
            """;

        var template = StarterContentReader.ReadTemplate(Bytes(json), "word").ShouldNotBeNull();

        template.Version.ShouldBe(3);
        template.Processes.ShouldBe([
            new ProcessName("winword.exe"),
            new ProcessName("winword32.exe"),
        ]);
        template
            .Shortcuts[0]
            .Action.ShouldBe(
                new TapAction(
                    KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.N),
                    [new ChordVariant(LangCode.En, KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.B))]
                )
            );
        var macro = template.Shortcuts[1].Action.ShouldBeOfType<MacroAction>();
        macro.Steps.Count.ShouldBe(4);
        macro.Steps[1].ShouldBeOfType<WaitStep>().Duration.ShouldBe(TimeSpan.FromMilliseconds(500));
        template
            .Shortcuts[2]
            .Action.ShouldBe(
                new UrlAction(new UrlTarget.Valid(new Uri("https://mail.example.org")))
            );
    }

    [Theory]
    [InlineData("other", """["winword.exe"]""", "an id that is not the file name")]
    [InlineData("word", """[]""", "no process")]
    [InlineData("word", """["c:\\office\\winword.exe"]""", "a process with a path")]
    [InlineData("word", """["winword.exe","WinWord.exe"]""", "a process repeated without case")]
    public void A_template_that_does_not_validate_gives_nothing(
        string expectedId,
        string processes,
        string because
    )
    {
        var json = $$$"""
            {"id":"word","version":1,"authors":["x"],"appsLanguages":["es"],"name":{"es":"Word","en":"Word"},"icon":"description",
             "processes":{{{processes}}},
             "shortcuts":[{"id":"s","name":{"es":"S","en":"S"},"icon":"bolt","category":"edit","action":{"type":"tap","keys":["f7"]}}]}
            """;

        StarterContentReader.ReadTemplate(Bytes(json), expectedId).ShouldBeNull(because);
    }

    [Fact]
    [Trait("Req", "BIE-006")]
    public void A_kit_keeps_its_order_defaults_and_texts()
    {
        const string json = """
            {"version":1,"options":[
              {"id":"basics","kind":"basics","selected":true,"icon":"apps","labelKey":"kitBasics","descriptionKey":"kitBasicsD"},
              {"id":"word","kind":"template","selected":false},
              {"id":"browser","kind":"template","selected":false}]}
            """;

        var kit = StarterContentReader.ReadKit(Bytes(json)).ShouldNotBeNull();

        kit.Options.Select(o => o.Id).ShouldBe(["basics", "word", "browser"]);
        kit.DefaultSelection.ShouldBe(StarterSelection.Of(["basics"]));
        kit.Options[0].Label.ShouldBe(L.KitBasics.Key);
        kit.Options[0].Description.ShouldBe(L.KitBasicsD.Key);
    }

    [Theory]
    [InlineData(
        """{"id":"basics","kind":"basics","selected":true,"icon":"apps","labelKey":"noSuchText","descriptionKey":"kitBasicsD"}""",
        "a text that does not exist"
    )]
    [InlineData(
        """{"id":"word","kind":"template","selected":false},{"id":"word","kind":"template","selected":true}""",
        "a repeated option and no «Basics»"
    )]
    [InlineData(
        """{"id":"basics","kind":"basics","selected":true,"icon":"apps","labelKey":"kitBasics","descriptionKey":"kitBasicsD"},{"id":"more","kind":"basics","selected":true,"icon":"apps","labelKey":"kitBasics","descriptionKey":"kitBasicsD"}""",
        "two «Basics»"
    )]
    [InlineData("""{"id":"basics","kind":"everything","selected":true}""", "an unknown kind")]
    public void A_kit_that_does_not_validate_gives_nothing(string options, string because) =>
        StarterContentReader
            .ReadKit(Bytes("{\"version\":1,\"options\":[" + options + "]}"))
            .ShouldBeNull(because);

    private static byte[] Bytes(string json) => Encoding.UTF8.GetBytes(json);
}
