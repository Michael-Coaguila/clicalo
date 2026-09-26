using System.Text;
using System.Text.Json.Nodes;
using Clicalo.DevCli.AnonymizeV1;

namespace Clicalo.DevCli.Tests.AnonymizeV1;

/// <summary>
/// <c>anonymize-v1</c> turns a real v1 file into a fixture without personal data (M2-ownership, «Privacidad de los
/// fixtures»): combinations, colours, counts and structure kept; names only when public; every other text replaced by
/// a placeholder of the same length and type.
/// </summary>
[Trait("Req", "MIG-004")]
public sealed class V1AnonymizerTests
{
    internal static readonly PublicNames Names = PublicNames.Parse(
        """{ "names": ["General", "Chrome", "Copiar", "Atrás"], "programs": ["chrome.exe", "cmd.exe", "notepad.exe"] }"""
    );

    internal const string Personal = """
        {
          "_nota": "Copia de Juan Pérez para su portátil",
          "active_profile": "Juan",
          "pinned_profile": "",
          "window_pos": [875, 134],
          "window_opacity": 0.68,
          "button_size": [55, 40],
          "profiles": {
            "General": {
              "process": "",
              "buttons_per_page": 9,
              "buttons": [
                { "label": "Copiar", "hotkey": "ctrl+c", "color": "#2980B9" },
                { "label": "Llamar a Mamá", "hotkey": "ctrl+shift+m", "color": "#55ff00" },
                { "label": "Correo", "type": "url", "action": "https://mail.example.com/u/juan.perez", "color": "#2980B9" },
                { "label": "Diario", "type": "app", "action": "\"C:\\Users\\juan\\AppData\\Local\\diario.exe\" --open C:\\Users\\juan\\notas.txt" },
                { "label": "Consola", "type": "app", "action": "cmd /c C:\\Users\\juan\\run.bat" },
                { "label": "Bloc", "type": "app", "action": "C:\\Users\\juan\\Tools\\notepad.exe" },
                { "type": "separator", "label": "" },
                { "label": "Llamar a Mamá", "action": "ctrl+alt+m", "color": "#55ff00" },
                { "label": "Atrás", "hotkey": "alt+left", "color": "#8E44AD", "nota": "de Juan" }
              ]
            },
            "Juan": { "process": "C:\\Users\\juan\\juanapp.exe", "buttons": [ { "label": "Copiar", "hotkey": "ctrl+c" } ] },
            "Chrome": { "process": "chrome.exe", "buttons": [] }
          }
        }
        """;

    private static readonly string[] Secrets =
    [
        "Juan",
        "juan",
        "Pérez",
        "portátil",
        "Mamá",
        "mail.example",
        "diario",
        "notas",
        "run.bat",
        "juanapp",
        "Correo",
        "Diario",
        "Consola",
        "Bloc",
    ];

    [Fact]
    public void Nothing_personal_is_left()
    {
        var text = Anonymize(Personal);

        foreach (var secret in Secrets)
        {
            text.ShouldNotContain(secret, Case.Sensitive);
        }
    }

    [Fact]
    public void Combinations_colours_numbers_and_structure_are_kept()
    {
        var original = JsonNode.Parse(Personal)!.AsObject();
        var output = JsonNode.Parse(Anonymize(Personal))!.AsObject();

        V1Anonymizer.Skeleton(output).ShouldBe(V1Anonymizer.Skeleton(original));
        output.Select(static p => p.Key).ShouldBe(original.Select(static p => p.Key));
        Buttons(output)
            .Select(static b => (string?)b["hotkey"])
            .ShouldBe(Buttons(original).Select(static b => (string?)b["hotkey"]));
        Buttons(output)
            .Select(static b => (string?)b["color"])
            .ShouldBe(Buttons(original).Select(static b => (string?)b["color"]));
        ((string?)Buttons(output)[7]["action"]).ShouldBe("ctrl+alt+m");
        output["window_pos"]!.ToJsonString().ShouldBe("[875,134]");
        ((double)output["window_opacity"]!).ShouldBe(0.68);
    }

    [Fact]
    public void Public_names_and_programs_are_kept_and_the_rest_replaced_with_the_same_shape()
    {
        var output = JsonNode.Parse(Anonymize(Personal))!.AsObject();
        var profiles = output["profiles"]!.AsObject().Select(static p => p.Key).ToList();
        var buttons = Buttons(output);

        profiles[0].ShouldBe("General");
        profiles[2].ShouldBe("Chrome");
        profiles[1].Length.ShouldBe("Juan".Length);
        char.IsUpper(profiles[1][0]).ShouldBeTrue();
        ((string?)output["active_profile"]).ShouldBe(profiles[1]);
        ((string?)output["pinned_profile"]).ShouldBe(string.Empty);
        ((string?)output["profiles"]!["Chrome"]!["process"]).ShouldBe("chrome.exe");

        ((string?)buttons[0]["label"]).ShouldBe("Copiar");
        ((string?)buttons[8]["label"]).ShouldBe("Atrás");
        var called = (string)buttons[1]["label"]!;
        called.Length.ShouldBe("Llamar a Mamá".Length);
        called
            .IndexOf(' ', StringComparison.Ordinal)
            .ShouldBe("Llamar a Mamá".IndexOf(' ', StringComparison.Ordinal));
        ((string?)buttons[7]["label"]).ShouldBe(called);
        string.Equals((string?)buttons[2]["label"], called, StringComparison.Ordinal)
            .ShouldBeFalse();
    }

    [Fact]
    public void Web_and_app_actions_keep_their_type_and_length()
    {
        var buttons = Buttons(JsonNode.Parse(Anonymize(Personal))!.AsObject());
        var originals = Buttons(JsonNode.Parse(Personal)!.AsObject());

        var url = (string)buttons[2]["action"]!;
        url.ShouldStartWith("https://");
        url.Length.ShouldBe(((string)originals[2]["action"]!).Length);

        var app = (string)buttons[3]["action"]!;
        app.Length.ShouldBe(((string)originals[3]["action"]!).Length);
        app[0].ShouldBe('"');
        app[2..4].ShouldBe(":\\");
        app.ShouldContain(".exe\" --open ");
        app.ShouldEndWith(".txt");

        ((string)buttons[4]["action"]!).ShouldStartWith("cmd /c ");
        ((string)buttons[4]["action"]!).ShouldEndWith(".bat");
        ((string)buttons[5]["action"]!).ShouldEndWith("\\notepad.exe");

        var process = (string)
            JsonNode.Parse(Anonymize(Personal))!["profiles"]!.AsObject().ElementAt(1).Value![
                "process"
            ]!;
        process.ShouldEndWith(".exe");
        process.Length.ShouldBe("C:\\Users\\juan\\juanapp.exe".Length);
    }

    [Fact]
    public void The_output_is_deterministic_indented_with_lf_and_keeps_non_ascii()
    {
        var first = Anonymize(Personal);

        Anonymize(Personal).ShouldBe(first);
        first.ShouldNotContain("\r", Case.Sensitive);
        first.ShouldContain("\n  \"profiles\": {", Case.Sensitive);
        first.ShouldContain("\"Atrás\"", Case.Sensitive);
    }

    [Fact]
    public void A_byte_order_mark_is_kept()
    {
        var input = new byte[] { 0xEF, 0xBB, 0xBF }
            .Concat(Encoding.UTF8.GetBytes(Personal))
            .ToArray();

        var (output, _) = new V1Anonymizer(Names).Anonymize(input);

        output.AsSpan(0, 3).ToArray().ShouldBe(new byte[] { 0xEF, 0xBB, 0xBF });
    }

    [Fact]
    public void The_counts_are_reported()
    {
        var (_, stats) = new V1Anonymizer(Names).Anonymize(Encoding.UTF8.GetBytes(Personal));

        stats.Profiles.ShouldBe(3);
        stats.Buttons.ShouldBe(10);
        stats.ReplacedTexts.ShouldBeGreaterThan(0);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1, 2]")]
    [InlineData("{ \"profiles\": { \"A\": {}, \"A\": {} } }")]
    public void Input_that_is_not_a_faithful_v1_file_is_refused(string text) =>
        Should.Throw<InvalidDataException>(() =>
            new V1Anonymizer(Names).Anonymize(Encoding.UTF8.GetBytes(text))
        );

    [Fact]
    public void Placeholders_are_unique_per_text_and_never_a_kept_name()
    {
        var placeholders = new Placeholders(static name =>
            string.Equals(name, "Xxxx", StringComparison.Ordinal)
        );

        var first = placeholders.For("Juan");
        placeholders.For("Juan").ShouldBe(first);
        string.Equals(placeholders.For("Pepe"), first, StringComparison.Ordinal).ShouldBeFalse();
        string.Equals(first, "Xxxx", StringComparison.Ordinal).ShouldBeFalse();
        first.Length.ShouldBe(4);
        placeholders.For("a1-b2").ShouldMatch("^[a-z][0-9]-[a-z][0-9]$");
        placeholders.For("--").ShouldBe("--");
    }

    internal static string Anonymize(string text) =>
        Encoding.UTF8.GetString(
            new V1Anonymizer(Names).Anonymize(Encoding.UTF8.GetBytes(text)).Output
        );

    private static List<JsonNode> Buttons(JsonObject document) =>
        [.. document["profiles"]!["General"]!["buttons"]!.AsArray().Select(static b => b!)];
}
