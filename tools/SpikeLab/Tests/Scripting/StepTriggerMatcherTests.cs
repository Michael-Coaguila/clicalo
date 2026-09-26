using Clicalo.Application.Foreground;
using Clicalo.Domain.Touch;
using Clicalo.Tools.SpikeLab.Scripting;

namespace Clicalo.Tools.SpikeLab.Tests.Scripting;

public sealed class StepTriggerMatcherTests
{
    private static readonly ScriptStep PenOnPanel = TestScripts.Tap("19") with
    {
        Pointer = PointerKind.Pen,
    };

    [Fact]
    public void A_tap_with_the_right_device_on_the_right_surface_counts() =>
        StepTriggerMatcher
            .Mismatch(PenOnPanel, Tap(SurfaceGroup.Panel, PointerKind.Pen))
            .ShouldBeNull();

    [Fact]
    public void A_tap_on_another_surface_does_not_count() =>
        StepTriggerMatcher
            .Mismatch(PenOnPanel, Tap(SurfaceGroup.Bubble, PointerKind.Pen))
            .ShouldBe("Este paso se hace sobre el panel.");

    [Fact]
    public void A_tap_with_another_device_does_not_count() =>
        StepTriggerMatcher
            .Mismatch(PenOnPanel, Tap(SurfaceGroup.Panel, PointerKind.Finger))
            .ShouldBe("Este paso se hace con el lápiz; el toque con el dedo no cuenta.");

    [Fact]
    public void A_promoted_mouse_tap_without_device_still_counts() =>
        StepTriggerMatcher
            .Mismatch(PenOnPanel, Tap(SurfaceGroup.Panel, pointer: null))
            .ShouldBeNull();

    [Fact]
    public void Another_kind_of_trigger_does_not_count() =>
        StepTriggerMatcher
            .Mismatch(PenOnPanel, Command("bold", CommandPattern.Invoke))
            .ShouldBe("Este paso no se cuenta con una orden de UI Automation.");

    [Fact]
    public void Nothing_automatic_counts_on_a_manual_step() =>
        StepTriggerMatcher
            .Mismatch(TestScripts.Manual("9a"), Tap(SurfaceGroup.Panel, PointerKind.Finger))
            .ShouldBe("Este paso se cuenta con «Funcionó» y «Falló».");

    [Fact]
    public void A_command_must_reach_the_tile_and_pattern_of_the_row()
    {
        var shift = new ScriptStep(
            "4",
            "Mayús",
            "Di «clic Mayús».",
            20,
            StepTrigger.UiaCommand,
            EvidenceCheck.NonActivation
        )
        {
            Tile = "shift",
            Pattern = CommandPattern.Toggle,
        };

        StepTriggerMatcher.Mismatch(shift, Command("shift", CommandPattern.Toggle)).ShouldBeNull();
        StepTriggerMatcher
            .Mismatch(shift, Command("bold", CommandPattern.Invoke))
            .ShouldNotBeNull();
        StepTriggerMatcher
            .Mismatch(shift, Command("shift", CommandPattern.Invoke))
            .ShouldNotBeNull();
    }

    [Fact]
    public void A_command_on_any_tile_counts_when_the_row_names_none()
    {
        var any = new ScriptStep(
            "7",
            "Narrador",
            "Doble toque.",
            20,
            StepTrigger.UiaCommand,
            EvidenceCheck.NonActivation
        );

        StepTriggerMatcher.Mismatch(any, Command("undo", CommandPattern.Invoke)).ShouldBeNull();
    }

    [Fact]
    public void A_lease_must_have_the_kind_and_origin_of_the_row()
    {
        var byVoice = new ScriptStep(
            "5",
            "Voz",
            "Di «clic Buscar».",
            20,
            StepTrigger.LeaseCycle,
            EvidenceCheck.LeaseRoundTrip
        )
        {
            Lease = LeaseKind.TextInput,
            Origin = LeaseOrigin.UiaInvoke,
        };

        StepTriggerMatcher
            .Mismatch(byVoice, Lease(LeaseKind.TextInput, LeaseOrigin.UiaInvoke))
            .ShouldBeNull();
        StepTriggerMatcher
            .Mismatch(byVoice, Lease(LeaseKind.TextInput, LeaseOrigin.Touch))
            .ShouldBe("Este paso espera el origen UiaInvoke.");
        StepTriggerMatcher
            .Mismatch(byVoice, Lease(LeaseKind.TrayMenu, LeaseOrigin.UiaInvoke))
            .ShouldBe("Este paso espera una concesión TextInput.");
    }

    private static RepetitionEvidence Tap(SurfaceGroup group, PointerKind? pointer) =>
        new()
        {
            Trigger = new TriggerInfo(StepTrigger.SurfaceTap) { Group = group, Pointer = pointer },
        };

    private static RepetitionEvidence Command(string tile, CommandPattern pattern) =>
        new()
        {
            Trigger = new TriggerInfo(StepTrigger.UiaCommand)
            {
                Group = SurfaceGroup.Panel,
                Tile = tile,
                Pattern = pattern,
            },
        };

    private static RepetitionEvidence Lease(LeaseKind kind, LeaseOrigin origin) =>
        new()
        {
            Trigger = new TriggerInfo(StepTrigger.LeaseCycle),
            Lease = new LeaseEvidence(kind, origin),
        };
}
