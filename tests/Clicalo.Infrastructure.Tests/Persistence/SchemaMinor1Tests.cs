using System.Text.Json;
using System.Text.Json.Nodes;
using Clicalo.Domain.Document;
using Clicalo.Domain.Settings;
using Clicalo.Infrastructure.Persistence;
using Clicalo.Infrastructure.Persistence.Dto;
using Clicalo.TestKit;
using PanelSize = Clicalo.Domain.Settings.PanelSize;

namespace Clicalo.Infrastructure.Tests.Persistence;

/// <summary>
/// Schema 1.1 (ADR-0028): the M6 settings and the welcome answers are optional members, so the immutable 1.1 fixture
/// reads with every value, a 1.0 document reads with their defaults, every field survives a round trip and a damaged
/// value is repaired, never fatal (REG-08).
/// </summary>
[Trait("Req", "DAT-001")]
public sealed class SchemaMinor1Tests
{
    private const string Monitor1 =
        @"\\?\DISPLAY#GSM5B7F#5&2c8a1e3f&0&UID4352#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";

    private const string Monitor2 =
        @"\\?\DISPLAY#BOE0A1C#4&1d2e3f40&0&UID8388688#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";

    [Fact]
    public void The_1_1_fixture_reads_with_a_matching_hash_and_no_unknown_members()
    {
        var read = Read("1.1");

        read.HashMatches.ShouldBeTrue();
        read.Envelope.Schema.ShouldBe(new SchemaVersion(1, 1));
        var dto = read.Envelope.Payload.Deserialize(DocumentJsonContext.Default.PayloadDto)!;
        dto.Extra.ShouldBeNull();
        dto.Settings!.Extra.ShouldBeNull();
        dto.Onboarding!.Extra.ShouldBeNull();
    }

    [Fact]
    [Trait("Req", "PES-016")]
    [Trait("Req", "CCM-001")]
    [Trait("Req", "BUR-005")]
    [Trait("Req", "ACC-006")]
    [Trait("Req", "BIE-010")]
    public void The_1_1_fixture_decodes_every_new_field_without_repairs()
    {
        var decoded = new DocumentCodec().Decode(Read("1.1").Envelope.Payload, live: false).Value;

        decoded.Repairs.ShouldBeEmpty();
        var settings = decoded.Document.Settings;
        settings.HandlePositionsByMonitor.ShouldBe([
            new MonitorHandlePosition(Monitor1, DockSide.Right, 30),
            new MonitorHandlePosition(Monitor1, DockSide.Bottom, 72),
            new MonitorHandlePosition(Monitor2, DockSide.Right, 64),
        ]);
        settings.ControlCenter.ShouldBe(
            new ControlCenterPlacement(Monitor2, -1240, 80.5, 1180, 720, Maximized: false)
        );
        settings.GlobalHotkey.ShouldBe(new GlobalHotkeySettings(Enabled: true, "ctrl-alt-f10"));
        settings.TimeMultiplier.ShouldBe(2);
        var onboarding = decoded.Document.Onboarding;
        onboarding.Completed.ShouldBeTrue();
        onboarding.Answers.ShouldBe(
            WelcomeAnswers.Create(
                [WelcomeAnswer.Touch, WelcomeAnswer.Voice],
                ["basics", "word"],
                new WelcomeBaseline("leve", PanelSize.Medium, VoiceNumbers: true, false)
            )
        );
        MonitorHandlePositions.PositionFor(settings, Monitor1, DockSide.Bottom).ShouldBe(72);
        MonitorHandlePositions.PositionFor(settings, Monitor2, DockSide.Left).ShouldBe(50);
    }

    [Fact]
    [Trait("Req", "PES-016")]
    [Trait("Req", "CCM-001")]
    [Trait("Req", "BUR-005")]
    [Trait("Req", "ACC-006")]
    [Trait("Req", "BIE-010")]
    public void A_1_0_document_reads_the_new_fields_with_their_defaults()
    {
        var decoded = new DocumentCodec().Decode(Read("1.0").Envelope.Payload, live: false).Value;

        decoded.Repairs.ShouldBeEmpty();
        var settings = decoded.Document.Settings;
        var defaults = SettingsSchema.Defaults;
        settings.HandlePositionsByMonitor.ShouldBeEmpty();
        settings.ControlCenter.ShouldBeNull();
        settings.GlobalHotkey.ShouldBe(defaults.GlobalHotkey);
        settings.GlobalHotkey.Enabled.ShouldBeFalse();
        settings.TimeMultiplier.ShouldBe(1);
        decoded.Document.Onboarding.ShouldBe(new OnboardingState(Completed: true));
        decoded.Document.Onboarding.Answers.ShouldBeNull();
        // Unknown monitor: the position per side of 1.0.
        MonitorHandlePositions
            .PositionFor(settings, Monitor1, DockSide.Right)
            .ShouldBe(settings.Dock.HandlePositions.Right);
    }

    [Fact]
    [Trait("Req", "PES-016")]
    [Trait("Req", "CCM-001")]
    [Trait("Req", "BUR-005")]
    [Trait("Req", "ACC-006")]
    [Trait("Req", "BIE-010")]
    public void Every_new_field_survives_a_round_trip()
    {
        var answers = WelcomeAnswers.Create(
            [WelcomeAnswer.Tremor, WelcomeAnswer.NoKeyboard],
            ["word", "basics", "excel"],
            new WelcomeBaseline("fuerte", PanelSize.Large, VoiceNumbers: false, true)
        );
        var document = TestDocuments.Document(1) with
        {
            Onboarding = new OnboardingState(Completed: true) { Answers = answers },
        };
        var codec = new DocumentCodec();

        var payload = codec.Encode(document, includeUsage: false);
        var decoded = codec.Decode(payload, live: false).Value;

        decoded.Repairs.ShouldBeEmpty();
        decoded.Document.Settings.ShouldBe(document.Settings);
        decoded.Document.Settings.TimeMultiplier.ShouldBe(2);
        decoded.Document.Settings.ControlCenter.ShouldBe(TestDocuments.Settings.ControlCenter);
        decoded.Document.Onboarding.ShouldBe(document.Onboarding);
        var settings = payload["settings"]!.AsObject();
        settings["handlePosByMonitor"]![1]!["side"]!.GetValue<string>().ShouldBe("bottom");
        settings["globalHotkey"]!["combo"]!.GetValue<string>().ShouldBe("ctrl-alt-f10");
        payload["onboarding"]!["uses"]!
            .AsArray()
            .Select(n => n!.GetValue<string>())
            .ShouldBe(["noKeyboard", "tremor"]);
        payload["onboarding"]!["kit"]!
            .AsArray()
            .Select(n => n!.GetValue<string>())
            .ShouldBe(["basics", "excel", "word"]);
        payload["onboarding"]!["baseline"]!["size"]!.GetValue<string>().ShouldBe("L");
    }

    [Fact]
    public void Absent_values_are_not_written()
    {
        var document = TestDocuments.Document(1) with
        {
            Settings = TestDocuments.Settings with { ControlCenter = null },
        };

        var payload = new DocumentCodec().Encode(document, includeUsage: false);

        payload["settings"]!.AsObject().ContainsKey("controlCenter").ShouldBeFalse();
        var onboarding = payload["onboarding"]!.AsObject();
        onboarding.ContainsKey("uses").ShouldBeFalse();
        onboarding.ContainsKey("kit").ShouldBeFalse();
        onboarding.ContainsKey("baseline").ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "DAT-003")]
    public void Damaged_new_values_are_repaired_or_dropped_never_fatal()
    {
        var payload = Read("1.1").Envelope.Payload;
        var settings = payload["settings"]!.AsObject();
        settings["timeMultiplier"] = 7;
        settings["globalHotkey"]!["combo"] = "ctrl-shift-m";
        settings["controlCenter"]!["width"] = -5;
        settings["handlePosByMonitor"] = new JsonArray(
            new JsonObject
            {
                ["monitor"] = Monitor1,
                ["side"] = "right",
                ["pos"] = 140,
            },
            new JsonObject
            {
                ["monitor"] = Monitor1,
                ["side"] = "right",
                ["pos"] = 20,
            },
            new JsonObject
            {
                ["monitor"] = "",
                ["side"] = "left",
                ["pos"] = 20,
            },
            new JsonObject
            {
                ["monitor"] = Monitor2,
                ["side"] = "diagonal",
                ["pos"] = 20,
            }
        );
        payload["onboarding"]!["baseline"]!["size"] = "XL";

        var decoded = new DocumentCodec().Decode(payload, live: false).Value;

        var repaired = decoded.Document.Settings;
        repaired.TimeMultiplier.ShouldBe(3);
        repaired.GlobalHotkey.Combo.ShouldBe(GlobalHotkeys.Default.Id);
        repaired.ControlCenter.ShouldBeNull();
        repaired.HandlePositionsByMonitor.ShouldBe([
            new MonitorHandlePosition(Monitor1, DockSide.Right, 92),
        ]);
        decoded.Document.Onboarding.Completed.ShouldBeTrue();
        decoded.Document.Onboarding.Answers.ShouldBeNull();
    }

    private static EnvelopeReadResult.Readable Read(string version) =>
        EnvelopeCodec
            .Read(
                File.ReadAllBytes(
                    RepoPaths.Combine(
                        "tests",
                        "Clicalo.Infrastructure.Tests",
                        "Fixtures",
                        "schema",
                        version,
                        "document.json"
                    )
                ),
                DocumentFormats.Document,
                DocumentFormats.DocumentSchema
            )
            .ShouldBeOfType<EnvelopeReadResult.Readable>();
}
