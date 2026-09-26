using System.Globalization;
using System.Text;
using Clicalo.Domain.Migration.V1;
using Clicalo.Domain.Timing;
using Clicalo.Infrastructure.Migration;

namespace Clicalo.Infrastructure.Tests.Migration;

/// <summary>The exact v1 schema of catalog §7.2, read tolerantly (MIG-002) and failing cleanly (EC-MIG-02).</summary>
public sealed class V1ReaderTests
{
    private const string Full = """
        {
          "active_profile": "Chrome",
          "pinned_profile": "General",
          "window_pos": [875, 134],
          "window_size": [239, 250],
          "edit_size": [391, 742],
          "window_opacity": 0.68,
          "button_size": [55, 40],
          "profiles": {
            "General": {
              "process": "",
              "buttons_per_page": 9,
              "buttons": [
                { "label": "Negrita", "hotkey": "ctrl+n", "color": "#2980B9" },
                { "label": "Guardar", "type": "hotkey", "hotkey": "ctrl+s", "color": "#27AE60" },
                { "label": "Rehacer", "action": "ctrl+y", "color": "#E67E22" },
                { "label": "Gmail", "type": "url", "action": "https://example.com", "color": "#2980B9" },
                { "label": "Notas", "type": "app", "action": "notepad.exe", "color": "#2980B9" },
                { "type": "separator", "label": "" }
              ]
            },
            "Chrome": { "process": "chrome.exe", "buttons": [] }
          }
        }
        """;

    [Fact]
    [Trait("Req", "MIG-002")]
    public void Every_key_and_every_button_variant_is_read()
    {
        var document = Read(Full).Value;

        document.ActiveProfile.ShouldBe("Chrome");
        document.PinnedProfile.ShouldBe("General");
        document.WindowPosition.ShouldBe(new V1Pair(875, 134));
        document.WindowSize.ShouldBe(new V1Pair(239, 250));
        document.EditSize.ShouldBe(new V1Pair(391, 742));
        document.WindowOpacity.ShouldBe(0.68);
        document.ButtonSize.ShouldBe(new V1Pair(55, 40));
        document.Profiles.Select(static p => p.Name).ShouldBe(["General", "Chrome"]);
        document.Profiles[1].Process.ShouldBe("chrome.exe");
        document.UnknownKeys.ShouldBeEmpty();
        document
            .Profiles[0]
            .Buttons.ShouldBe([
                new V1Button(
                    V1ButtonKind.ImplicitHotkey,
                    "Negrita",
                    "ctrl+n",
                    null,
                    "#2980B9",
                    null
                ),
                new V1Button(
                    V1ButtonKind.ExplicitHotkey,
                    "Guardar",
                    "ctrl+s",
                    null,
                    "#27AE60",
                    "hotkey"
                ),
                new V1Button(V1ButtonKind.ActionHotkey, "Rehacer", "ctrl+y", null, "#E67E22", null),
                new V1Button(
                    V1ButtonKind.Url,
                    "Gmail",
                    null,
                    "https://example.com",
                    "#2980B9",
                    "url"
                ),
                new V1Button(V1ButtonKind.App, "Notas", null, "notepad.exe", "#2980B9", "app"),
                new V1Button(V1ButtonKind.Separator, string.Empty, null, null, null, "separator"),
            ]);
        V1Counts.Of(document).ShouldBe(new V1Counts(2, 6, 1, 1, 1));
    }

    [Fact]
    [Trait("Req", "MIG-002")]
    public void Missing_keys_are_left_empty_for_the_v1_defaults()
    {
        var document = Read("""{ "profiles": { "General": {} } }""").Value;

        document.ActiveProfile.ShouldBeNull();
        document.WindowPosition.ShouldBeNull();
        document.WindowOpacity.ShouldBeNull();
        document.ButtonSize.ShouldBeNull();
        var general = document.Profiles.ShouldHaveSingleItem();
        general.Process.ShouldBeEmpty();
        general.ButtonsPerPage.ShouldBeNull();
        general.Buttons.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "MIG-002")]
    public void Unknown_keys_are_tolerated_and_named_for_the_report()
    {
        var document = Read(
            """{ "_nota": "texto", "profiles": { "General": { "extra": 1, "buttons": [ { "label": "A", "hotkey": "a", "icon": "x" } ] } }, "zoom": 2 }"""
        ).Value;

        document.UnknownKeys.ShouldBe(["_nota", "zoom"]);
        document.Profiles[0].Buttons.ShouldHaveSingleItem().Label.ShouldBe("A");
    }

    [Fact]
    [Trait("Req", "MIG-002")]
    public void A_byte_order_mark_and_crlf_are_tolerated()
    {
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }
            .Concat(
                Encoding.UTF8.GetBytes(
                    "{\r\n  \"profiles\": {\r\n    \"General\": {}\r\n  }\r\n}\r\n"
                )
            )
            .ToArray();

        V1Reader.Read(bytes).Value.Profiles.ShouldHaveSingleItem().Name.ShouldBe("General");
    }

    [Fact]
    [Trait("Req", "MIG-002")]
    public void Values_of_the_wrong_type_count_as_missing()
    {
        var document = Read(
            """{ "active_profile": 3, "window_pos": "80,80", "window_opacity": "0.9", "button_size": [55], "profiles": { "General": { "process": null, "buttons_per_page": "9", "buttons": [ 7, { "label": 12, "hotkey": ["ctrl"] } ] } } }"""
        ).Value;

        document.ActiveProfile.ShouldBeNull();
        document.WindowPosition.ShouldBeNull();
        document.WindowOpacity.ShouldBeNull();
        document.ButtonSize.ShouldBeNull();
        document.Profiles[0].Process.ShouldBeEmpty();
        document.Profiles[0].ButtonsPerPage.ShouldBeNull();

        // A button that is not an object is not a button; a numeric label is kept as text, as v1 showed it.
        var button = document.Profiles[0].Buttons.ShouldHaveSingleItem();
        button.Label.ShouldBe("12");
        button.Hotkey.ShouldBeNull();
    }

    [Fact]
    [Trait("Req", "MIG-002")]
    public void Integers_written_as_decimals_still_count()
    {
        var document = Read(
            """{ "window_pos": [80.0, 79.6], "profiles": { "General": {} } }"""
        ).Value;

        document.WindowPosition.ShouldBe(new V1Pair(80, 80));
    }

    [Fact]
    [Trait("Req", "MIG-002")]
    public void A_repeated_key_keeps_the_first_position_and_the_last_value_like_v1()
    {
        var document = Read(
            """{ "profiles": { "General": { "process": "" }, "Chrome": { "process": "chrome.exe" }, "General": { "process": "", "buttons": [ { "label": "A", "hotkey": "a" } ] } }, "active_profile": "General", "active_profile": "Chrome" }"""
        ).Value;

        document.Profiles.Select(static p => p.Name).ShouldBe(["General", "Chrome"]);
        document.Profiles[0].Buttons.ShouldHaveSingleItem();
        document.ActiveProfile.ShouldBe("Chrome");
    }

    [Fact]
    [Trait("Req", "MIG-002")]
    public void Type_names_are_read_without_case_and_other_types_are_unknown()
    {
        var document = Read(
            """{ "profiles": { "General": { "buttons": [ { "label": "W", "type": " URL ", "action": "https://example.com" }, { "label": "M", "type": "macro", "hotkey": "ctrl+s" }, { "label": "X", "type": "macro", "action": "ctrl+x" } ] } } }"""
        ).Value;

        var buttons = document.Profiles[0].Buttons;
        buttons[0].Kind.ShouldBe(V1ButtonKind.Url);
        buttons[1].ShouldBe(new V1Button(V1ButtonKind.Unknown, "M", "ctrl+s", null, null, "macro"));
        buttons[2].ShouldBe(new V1Button(V1ButtonKind.Unknown, "X", "ctrl+x", null, null, "macro"));
    }

    [Theory]
    [Trait("Req", "MIG-004")]
    [InlineData("", V1ImportFailures.EmptyCode)]
    [InlineData("   \r\n", V1ImportFailures.EmptyCode)]
    [InlineData("{ \"profiles\": { \"General\": {", V1ImportFailures.DamagedCode)]
    [InlineData("{ \"profiles\": {} } trailing", V1ImportFailures.DamagedCode)]
    [InlineData("[1, 2, 3]", V1ImportFailures.NotV1Code)]
    [InlineData("\"profiles\"", V1ImportFailures.NotV1Code)]
    [InlineData("{ \"active_profile\": \"General\" }", V1ImportFailures.NoProfilesCode)]
    [InlineData("{ \"profiles\": [] }", V1ImportFailures.NoProfilesCode)]
    [InlineData("{ \"profiles\": {} }", V1ImportFailures.NoProfilesCode)]
    public void A_file_that_is_empty_truncated_or_without_profiles_fails_with_retry(
        string text,
        string code
    )
    {
        var result = Read(text);

        result.Failure.Code.ShouldBe(code);
        result.Failure.Recovery.ShouldBe(Domain.Errors.FailureRecovery.Retry);
    }

    [Fact]
    [Trait("Req", "MIG-004")]
    public void Text_that_is_not_utf8_fails_cleanly()
    {
        var bytes = Encoding.Latin1.GetBytes("{ \"profiles\": { \"Diseño\": {} } }");

        Read(bytes).Failure.Code.ShouldBe(V1ImportFailures.DamagedCode);
    }

    [Fact]
    [Trait("Req", "LOG-006")]
    public void Size_depth_profiles_and_buttons_are_bounded()
    {
        var tooLarge = new byte[Timings.Import.V1MaxBytes + 1];
        Read(tooLarge).Failure.Code.ShouldBe(V1ImportFailures.TooLargeCode);

        var deep =
            "{ \"profiles\": { \"General\": { \"x\": "
            + new string('[', 40)
            + new string(']', 40)
            + " } } }";
        Read(deep).Failure.Code.ShouldBe(V1ImportFailures.DamagedCode);

        var profiles = string.Join(
            ", ",
            Enumerable
                .Range(0, Timings.Import.ShareMaxProfiles + 1)
                .Select(static i => string.Create(CultureInfo.InvariantCulture, $"\"P{i}\": {{}}"))
        );
        Read("{ \"profiles\": { " + profiles + " } }")
            .Failure.Code.ShouldBe(V1ImportFailures.TooLargeCode);

        var buttons = string.Join(
            ", ",
            Enumerable.Repeat("{}", Timings.Import.ShareMaxShortcuts + 1)
        );
        Read("{ \"profiles\": { \"General\": { \"buttons\": [" + buttons + "] } } }")
            .Failure.Code.ShouldBe(V1ImportFailures.TooLargeCode);
    }

    private static Domain.Errors.Result<V1Document> Read(string text) =>
        V1Reader.Read(Encoding.UTF8.GetBytes(text));

    private static Domain.Errors.Result<V1Document> Read(byte[] bytes) => V1Reader.Read(bytes);
}
