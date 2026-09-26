using Clicalo.Application.Foreground;
using Clicalo.Domain.Touch;
using Clicalo.TestKit;
using Clicalo.Tools.SpikeLab.Scripting;
using Clicalo.Tools.SpikeLab.Tiles;

namespace Clicalo.Tools.SpikeLab.Tests.Scripting;

/// <summary>
/// The scripts mirror the manual results tables of docs/testing/spikes/S1.md, S3.md and S4.md: if a document adds or
/// removes a row, these tests fail until the catalog follows.
/// </summary>
public sealed class SpikeScriptsTests
{
    public static TheoryData<string, string> Rows =>
        new()
        {
            { "S1", string.Join(',', Enumerable.Range(1, 32)) },
            { "S3", "1,2,3,4,5,6,7,8,9a,9b,10,11,12,13" },
            { "S4", string.Join(',', Enumerable.Range(1, 11)) },
        };

    [Theory]
    [MemberData(nameof(Rows))]
    public void Each_script_mirrors_the_rows_of_its_results_table(string spike, string rows)
    {
        var script = SpikeScripts.For(Enum.Parse<SpikeId>(spike));

        string.Join(',', script.Steps.Select(step => step.Id)).ShouldBe(rows);
        script.Document.ShouldBe("docs/testing/spikes/" + spike + ".md");
        File.Exists(RepoPaths.Combine(script.Document)).ShouldBeTrue();
    }

    [Fact]
    public void Every_row_needs_twenty_repetitions_except_the_high_contrast_row()
    {
        foreach (var script in AllScripts())
        {
            foreach (var step in script.Steps)
            {
                var expected =
                    script.Id == SpikeId.S3
                    && string.Equals(step.Id, "13", StringComparison.Ordinal)
                        ? SpikeScripts.HighContrastRepetitions
                        : SpikeScripts.CyclesPerRow;
                step.Required.ShouldBe(expected, script.Id + " row " + step.Id);
            }
        }

        SpikeScripts.CyclesPerRow.ShouldBe(20);
        SpikeScripts.HighContrastRepetitions.ShouldBe(5);
    }

    [Fact]
    public void Every_step_says_what_to_do_and_how_it_is_counted()
    {
        foreach (var script in AllScripts())
        {
            foreach (var step in script.Steps)
            {
                var name = script.Id + " row " + step.Id;
                step.Title.ShouldNotBeNullOrWhiteSpace(name);
                step.Instruction.Length.ShouldBeGreaterThan(40, name);
                switch (step.Trigger)
                {
                    case StepTrigger.SurfaceTap or StepTrigger.HandleDrag:
                        step.Surface.ShouldNotBe(SurfaceGroup.Any, name);
                        step.Checks.ShouldBe(EvidenceCheck.NonActivation, name);
                        break;
                    case StepTrigger.UiaCommand when step.Tile is { } tile:
                        LabTiles.VoiceNumberOf(tile).ShouldNotBeNull(name);
                        break;
                    case StepTrigger.LeaseCycle:
                        step.Lease.ShouldNotBeNull(name);
                        step.Origin.ShouldNotBeNull(name);
                        step.Checks.HasFlag(EvidenceCheck.LeaseRoundTrip).ShouldBeTrue(name);
                        break;
                }
            }
        }
    }

    [Fact]
    public void Only_the_rows_that_depend_on_missing_hardware_or_a_virtual_machine_are_optional()
    {
        Optional(SpikeScripts.S1).ShouldBe(["19", "20", "21", "22", "23", "24", "26", "30"]);
        Optional(SpikeScripts.S3).ShouldBe(["8", "11", "12"]);
        Optional(SpikeScripts.S4).ShouldBe(["7"]);
    }

    [Fact]
    public void Only_the_rows_that_do_not_decide_their_spike_are_marked_so()
    {
        NotDecisive(SpikeScripts.S1).ShouldBe(["32"]);
        NotDecisive(SpikeScripts.S3).ShouldBe(["8"]);
        NotDecisive(SpikeScripts.S4).ShouldBeEmpty();
    }

    [Fact]
    public void S1_covers_three_surfaces_on_six_apps_with_the_finger_then_pen_and_mouse()
    {
        var taps = SpikeScripts.S1.Steps.Take(24).ToArray();

        taps.ShouldAllBe(step => step.Trigger == StepTrigger.SurfaceTap);
        taps.Take(18).ShouldAllBe(step => step.Pointer == PointerKind.Finger);
        taps.Skip(18).Take(3).ShouldAllBe(step => step.Pointer == PointerKind.Pen);
        taps.Skip(21).ShouldAllBe(step => step.Pointer == PointerKind.Mouse);
        taps.Select(step => step.Surface)
            .Take(3)
            .ShouldBe([SurfaceGroup.Panel, SurfaceGroup.TabWithSide, SurfaceGroup.Bubble]);
    }

    [Fact]
    public void The_forced_activation_row_is_counted_by_its_own_button()
    {
        var forced = SpikeScripts.S1.Steps.Single(step =>
            string.Equals(step.Id, "31", StringComparison.Ordinal)
        );

        forced.Trigger.ShouldBe(StepTrigger.ForcedActivation);
        forced.Action.ShouldBe(StepAction.ForceActivation);
        forced.Checks.HasFlag(EvidenceCheck.ForcedActivationReverted).ShouldBeTrue();
    }

    [Fact]
    public void The_notice_rows_of_S3_are_manual_with_their_buttons_on_the_guide_strip()
    {
        var notices = SpikeScripts.S3.Steps.Where(step => step.Id.StartsWith('9')).ToArray();

        notices
            .Select(step => step.Action)
            .ShouldBe([StepAction.PoliteNotice, StepAction.AssertiveNotice]);
        notices.ShouldAllBe(step => step.Trigger == StepTrigger.Manual);
    }

    [Fact]
    public void S3_asks_for_the_voice_number_in_the_name_where_the_row_uses_Clicalo_numbers()
    {
        var row = SpikeScripts.S3.Steps.Single(step =>
            string.Equals(step.Id, "6", StringComparison.Ordinal)
        );

        row.Checks.HasFlag(EvidenceCheck.VoiceNumberInName).ShouldBeTrue();
        LabTiles.VoiceNumberOf(row.Tile!).ShouldBe(7);
        row.Action.ShouldBe(
            StepAction.ToggleVoiceNumbers,
            "the numbers are switched from the strip"
        );
        row.Instruction.ShouldContain("di «ocultar números»");
    }

    [Fact]
    public void The_voice_row_of_S1_counts_the_order_on_the_tile_it_names()
    {
        var row = SpikeScripts.S1.Steps[^1];

        row.Id.ShouldBe("32");
        row.Tile.ShouldBe(LabTiles.Underline);
        row.Instruction.ShouldContain("«clic Subrayado»");
    }

    [Fact]
    public void The_rows_with_numbers_say_how_to_show_and_hide_them()
    {
        foreach (var id in (string[])["1", "3", "13"])
        {
            var row = SpikeScripts.S3.Steps.Single(step =>
                string.Equals(step.Id, id, StringComparison.Ordinal)
            );
            row.Instruction.ShouldContain("«ocultar números»", customMessage: "S3 row " + id);
        }
    }

    [Fact]
    public void The_rows_with_Wispr_Flow_or_Typeless_wait_for_the_text()
    {
        foreach (var step in SpikeScripts.All.SelectMany(script => script.Steps))
        {
            if (
                System.Text.RegularExpressions.Regex.IsMatch(
                    step.Instruction,
                    "\\b[Dd]icta\\b",
                    System.Text.RegularExpressions.RegexOptions.None,
                    TimeSpan.FromSeconds(1)
                )
                && (
                    step.Instruction.Contains("Wispr Flow", StringComparison.Ordinal)
                    || step.Instruction.Contains("Typeless", StringComparison.Ordinal)
                )
            )
            {
                step.Instruction.ShouldContain("espera", Case.Insensitive, "row " + step.Id);
            }
        }

        foreach (var step in SpikeScripts.S4.Steps.Take(4))
        {
            step.Instruction.ShouldContain("documento en blanco");
            step.Instruction.ShouldContain(
                "Espera a ver «negr» o «negrita» en el campo de búsqueda"
            );
        }
    }

    [Fact]
    public void S4_opens_the_search_from_every_origin_and_the_control_center_from_panel_and_tray()
    {
        SpikeScripts
            .S4.Steps.Select(step => (step.Lease, step.Origin))
            .Distinct()
            .ShouldBe([
                (LeaseKind.TextInput, LeaseOrigin.Touch),
                (LeaseKind.TextInput, LeaseOrigin.UiaInvoke),
                (LeaseKind.TextInput, LeaseOrigin.GlobalHotkey),
                (LeaseKind.ControlCenter, LeaseOrigin.Touch),
                (LeaseKind.ControlCenter, LeaseOrigin.Tray),
                (LeaseKind.TrayMenu, LeaseOrigin.Tray),
            ]);
        SpikeScripts
            .S4.Steps.Where(step =>
                step.Origin == LeaseOrigin.UiaInvoke || step.Origin == LeaseOrigin.GlobalHotkey
            )
            .ShouldAllBe(step =>
                step.Checks.HasFlag(EvidenceCheck.ProbeSilent | EvidenceCheck.ProbeWasTarget)
            );
    }

    private static SpikeScript[] AllScripts() =>
        [SpikeScripts.S1, SpikeScripts.S3, SpikeScripts.S4];

    private static string[] Optional(SpikeScript script) =>
        [.. script.Steps.Where(step => step.Optional).Select(step => step.Id)];

    private static string[] NotDecisive(SpikeScript script) =>
        [.. script.Steps.Where(step => !step.Decisive).Select(step => step.Id)];
}
