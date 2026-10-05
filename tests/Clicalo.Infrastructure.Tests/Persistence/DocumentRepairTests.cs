using Clicalo.Infrastructure.Persistence.Dto;
using Clicalo.Infrastructure.Persistence.Mappers;

namespace Clicalo.Infrastructure.Tests.Persistence;

/// <summary>What is repaired before the Domain validates a document (blueprint §6.5, §6.2 invariants I1 to I6).</summary>
[Trait("Req", "DAT-003")]
[Trait("Req", "DAT-005")]
public sealed class DocumentRepairTests
{
    [Fact]
    public void A_valid_payload_is_left_as_it_is()
    {
        var (payload, repairs) = DocumentRepair.Apply(
            Payload([Profile("general", Shortcut("copy")), Profile("word", Shortcut("bold"))])
        );

        repairs.ShouldBeEmpty();
        payload.Profiles!.Select(p => p.Id).ShouldBe(["general", "word"]);
    }

    [Fact]
    [Trait("Req", "DAT-004")]
    public void Duplicate_and_missing_ids_get_new_unique_ids_and_nothing_is_dropped()
    {
        var (payload, repairs) = DocumentRepair.Apply(
            Payload(
                [
                    Profile("general", Shortcut("copy"), Shortcut("copy"), Shortcut(null)),
                    Profile("word", Shortcut("copy")),
                ],
                always: [Shortcut("word")]
            )
        );

        repairs.ShouldBe([DocumentRepair.DuplicateId, DocumentRepair.MissingId]);
        var ids = payload
            .Profiles!.SelectMany(p => p.Shortcuts!.Select(s => s.Id))
            .Concat(payload.Profiles!.Select(p => p.Id))
            .Concat(payload.Always!.Shortcuts!.Select(s => s.Id))
            .ToList();
        ids.Count.ShouldBe(7);
        ids.Distinct(StringComparer.Ordinal).Count().ShouldBe(ids.Count);
        payload.Profiles![0].Shortcuts![0].Id.ShouldBe("copy");
    }

    [Fact]
    public void A_missing_general_is_created_first_and_empty()
    {
        var (payload, repairs) = DocumentRepair.Apply(Payload([Profile("word")]));

        repairs.ShouldBe([DocumentRepair.GeneralMissing]);
        payload.Profiles![0].Id.ShouldBe("general");
        payload.Profiles![0].Shortcuts.ShouldBeEmpty();
        payload.Profiles![1].Id.ShouldBe("word");
    }

    [Fact]
    public void General_never_follows_a_process()
    {
        var (payload, repairs) = DocumentRepair.Apply(
            Payload([Profile("general") with { Processes = ["explorer.exe"] }])
        );

        repairs.ShouldBe([DocumentRepair.GeneralBound]);
        payload.Profiles![0].Processes.ShouldBeNull();
    }

    [Fact]
    [Trait("Req", "PER-002")]
    public void A_process_bound_twice_keeps_its_first_profile_without_case()
    {
        var (payload, repairs) = DocumentRepair.Apply(
            Payload([
                Profile("general"),
                Profile("word") with
                {
                    Processes = ["WINWORD.EXE", "winword.exe"],
                },
                Profile("other") with
                {
                    Processes = ["Winword.exe", "excel.exe"],
                },
            ])
        );

        repairs.ShouldBe([DocumentRepair.ProcessRebound]);
        payload.Profiles![1].Processes.ShouldBe(["WINWORD.EXE"]);
        payload.Profiles![2].Processes.ShouldBe(["excel.exe"]);
    }

    [Theory]
    [Trait("Req", "EJE-010")]
    [InlineData(5.0, 100.0)]
    [InlineData(999_999.0, 10_000.0)]
    public void A_wait_out_of_range_is_clamped(double ms, double expected)
    {
        var macro = Shortcut("m") with
        {
            Action = new ActionDto
            {
                Type = "macro",
                Steps = [new StepDto { Kind = "wait", Ms = ms }],
            },
        };

        var (payload, repairs) = DocumentRepair.Apply(Payload([Profile("general", macro)]));

        repairs.ShouldBe([DocumentRepair.WaitRange]);
        payload.Profiles![0].Shortcuts![0].Action!.Steps![0].Ms.ShouldBe(expected);
    }

    [Fact]
    [Trait("Req", "PER-004")]
    public void A_dangling_last_profile_is_cleared()
    {
        var (payload, repairs) = DocumentRepair.Apply(
            Payload([Profile("general")]) with
            {
                Settings = new SettingsDto { LastProfile = "gone" },
            }
        );

        repairs.ShouldBe([DocumentRepair.LastProfile]);
        payload.Settings!.LastProfile.ShouldBeNull();
    }

    [Fact]
    [Trait("Req", "FRE-005")]
    public void Dangling_pins_are_kept_on_purpose_and_repeated_ones_are_removed()
    {
        var (payload, repairs) = DocumentRepair.Apply(
            Payload([Profile("general")]) with
            {
                Frequents = new FrequentsDto
                {
                    Pins = ["gone", "gone", "also-gone"],
                    Hidden = ["x"],
                },
            }
        );

        repairs.ShouldBe([DocumentRepair.DuplicatePin]);
        payload.Frequents!.Pins.ShouldBe(["gone", "also-gone"]);
        payload.Frequents!.Hidden.ShouldBe(["x"]);
    }

    private static PayloadDto Payload(
        List<ProfileDto> profiles,
        List<ShortcutDto>? always = null
    ) =>
        new()
        {
            Profiles = profiles,
            Always = new ShortcutListDto { Shortcuts = always ?? [] },
        };

    private static ProfileDto Profile(string id, params ShortcutDto[] shortcuts) =>
        new()
        {
            Id = id,
            Name = new(StringComparer.Ordinal) { ["es"] = id, ["en"] = id },
            Shortcuts = [.. shortcuts],
        };

    private static ShortcutDto Shortcut(string? id) =>
        new()
        {
            Id = id,
            Action = new ActionDto { Type = "tap", Keys = ["ctrl", "c"] },
        };
}
