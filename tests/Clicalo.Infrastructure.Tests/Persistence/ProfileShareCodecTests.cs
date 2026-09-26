using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Privacy;
using Clicalo.Infrastructure.Persistence;
using Clicalo.TestKit.Time;

namespace Clicalo.Infrastructure.Tests.Persistence;

/// <summary>Sharing one profile (DAT-007): texts left out by default, untrusted on import (LOG-006, LOG-008).</summary>
[Trait("Req", "DAT-007")]
public sealed class ProfileShareCodecTests
{
    private static readonly ProfileShareCodec Codec = new(TestTime.CreateProvider());

    [Fact]
    public void Exports_one_profile_with_its_type_and_schema_and_without_its_texts()
    {
        var profile = SharedProfile();

        var export = Codec.Export(profile, includeTextsInClear: false);

        export.FileName.ShouldBe("clicalo-perfil-mail.json");
        export.ExcludedTexts.ShouldBe(2);
        var text = Encoding.UTF8.GetString(export.Content.Span);
        var root = JsonNode.Parse(text)!;
        root["type"]!.GetValue<string>().ShouldBe("profile-share");
        root["schema"]!["major"]!.GetValue<int>().ShouldBe(1);
        root["profile"]!["id"]!.GetValue<string>().ShouldBe("mail");
        text.ShouldNotContain("Saludos");
        text.ShouldNotContain("PRIVADO");
        text.ShouldNotContain("dpapi");
        text.ShouldNotContain("pinnedFrom");
    }

    [Fact]
    [Trait("Req", "LOG-003")]
    public void Texts_are_included_in_clear_only_when_the_user_chooses_it()
    {
        var export = Codec.Export(SharedProfile(), includeTextsInClear: true);

        export.ExcludedTexts.ShouldBe(0);
        Encoding.UTF8.GetString(export.Content.Span).ShouldContain("Saludos");
    }

    [Theory]
    [InlineData("Word 2024!", "clicalo-perfil-Word-2024-.json")]
    [InlineData("", "clicalo-perfil-shared.json")]
    [InlineData("../../x", "clicalo-perfil-------x.json")]
    public void The_file_name_never_carries_path_characters(string id, string expected) =>
        ProfileShareCodec.FileName(new ProfileId(id)).ShouldBe(expected);

    [Fact]
    [Trait("Req", "DAT-004")]
    [Trait("Req", "LOG-008")]
    [Trait("Req", "COP-005")]
    public void Importing_gives_new_ids_marks_left_out_texts_and_counts_risky_actions()
    {
        var export = Codec.Export(SharedProfile(), includeTextsInClear: false);

        var import = ProfileShareCodec.Import(export.Content.Span, new SequentialIds()).Value;

        import.Profile.Id.ShouldBe(new ProfileId("p1"));
        import.Profile.Shortcuts.Select(s => s.Id.Value).ShouldBe(["s1", "s2", "s3"]);
        import.Profile.Shortcuts.ShouldAllBe(s => s.PinnedFrom == null);
        import.UnavailableTexts.ShouldBe(2);
        import.RiskyShortcuts.ShouldBe(2);
        import
            .Profile.Shortcuts[0]
            .Action.ShouldBeOfType<TextAction>()
            .Text.IsAvailable.ShouldBeFalse();
    }

    [Fact]
    public void Texts_shared_in_clear_arrive_as_texts()
    {
        var export = Codec.Export(SharedProfile(), includeTextsInClear: true);

        var import = ProfileShareCodec.Import(export.Content.Span, new SequentialIds()).Value;

        import.UnavailableTexts.ShouldBe(0);
        import
            .Profile.Shortcuts[0]
            .Action.ShouldBeOfType<TextAction>()
            .Text.ShouldBe(SecretText.From("Saludos, M."));
    }

    [Theory]
    [Trait("Req", "LOG-006")]
    [InlineData("{", "import.unreadable")]
    [InlineData("[]", "import.unreadable")]
    [InlineData("{\"type\":\"template\",\"profile\":{}}", "import.unreadable")]
    [InlineData("{\"type\":\"profile-share\"}", "import.unreadable")]
    [InlineData(
        "{\"type\":\"profile-share\",\"schema\":{\"major\":2,\"minor\":0},\"profile\":{}}",
        "import.schema_newer"
    )]
    [InlineData(
        "{\"type\":\"profile-share\",\"profile\":{\"shortcuts\":[{\"id\":\"a\"}]}}",
        "import.invalid"
    )]
    [InlineData(
        "{\"type\":\"profile-share\",\"profile\":{\"id\":\"x\",\"shortcuts\":[{\"id\":\"a\",\"action\":{\"type\":\"exec\"}}]}}",
        "import.invalid"
    )]
    public void Hostile_or_foreign_files_are_refused(string json, string code) =>
        ProfileShareCodec
            .Import(Encoding.UTF8.GetBytes(json), new SequentialIds())
            .Failure.Code.ShouldBe(code);

    [Fact]
    [Trait("Req", "LOG-006")]
    public void A_file_over_the_size_limit_is_refused_before_parsing()
    {
        var big = new byte[(5 * 1024 * 1024) + 1];

        ProfileShareCodec
            .Import(big, new SequentialIds())
            .Failure.Code.ShouldBe("import.too_large");
    }

    [Fact]
    [Trait("Req", "LOG-006")]
    public void A_file_nested_deeper_than_the_limit_is_refused()
    {
        var json =
            "{\"type\":\"profile-share\",\"profile\":"
            + new string('[', 40)
            + new string(']', 40)
            + "}";

        ProfileShareCodec
            .Import(Encoding.UTF8.GetBytes(json), new SequentialIds())
            .Failure.Code.ShouldBe("import.unreadable");
    }

    private static Profile SharedProfile() =>
        TestDocuments.Profile(
            "mail",
            [
                TestDocuments.Text("sig", "Saludos, M.") with
                {
                    PinnedFrom = new ProfileId("general"),
                },
                TestDocuments.Url("web", "https://mail.example.org"),
                TestDocuments.Shortcut(
                    "m",
                    new MacroAction([
                        new TextStep(SecretText.From("PRIVADO")),
                        new MouseStep(MouseOp.DoubleClick),
                    ])
                ),
            ],
            "outlook.exe"
        );

    /// <summary>Ids p1, p2… and s1, s2… in order.</summary>
    private sealed class SequentialIds : IIdGenerator
    {
        private int _profiles;
        private int _shortcuts;

        public ProfileId NewProfileId() =>
            new("p" + (++_profiles).ToString(CultureInfo.InvariantCulture));

        public ShortcutId NewShortcutId() =>
            new("s" + (++_shortcuts).ToString(CultureInfo.InvariantCulture));
    }
}
