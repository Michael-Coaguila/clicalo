using Clicalo.Application.Engine;
using Clicalo.Application.Interaction;
using Clicalo.Domain.Execution;
using Clicalo.Domain.KeySafety;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;

namespace Clicalo.Application.Tests.Interaction;

/// <summary>
/// <see cref="EngineNoticeRules"/> (AVI-002): which notices of the engine are safety notices, and the fixed notices of a
/// Mantener under a finger and of a running macro, which last exactly as long as the engine's snapshot says.
/// </summary>
public sealed class EngineNoticeRulesTests
{
    private static readonly ShortcutId Shortcut = new("copy");

    [Fact]
    [Trait("Req", "AVI-002")]
    public void The_reasons_keys_were_released_and_the_elevated_refusal_are_safety_notices()
    {
        EngineNoticeRules.KindOf(L.ReleasedAuto(count: 30)).ShouldBe(NoticeKind.Safety);
        EngineNoticeRules.KindOf(L.ReleasedSwitch).ShouldBe(NoticeKind.Safety);
        EngineNoticeRules.KindOf(L.ReleasedOnLock).ShouldBe(NoticeKind.Safety);
        EngineNoticeRules.KindOf(L.EngineFault).ShouldBe(NoticeKind.Safety);
        EngineNoticeRules.KindOf(L.ElevatedRefused(app: "regedit")).ShouldBe(NoticeKind.Safety);

        EngineNoticeRules.KindOf(L.Released).ShouldBe(NoticeKind.Normal);
        EngineNoticeRules.KindOf(L.ConfirmCloseName(name: "Cerrar")).ShouldBe(NoticeKind.Normal);
        EngineNoticeRules
            .KindOf(L.MacroRanName(name: "Informe", total: 3))
            .ShouldBe(NoticeKind.Normal);
    }

    [Fact]
    [Trait("Req", "AVI-002")]
    public void The_notices_of_a_state_that_lasts_are_told_by_the_fixed_notice()
    {
        EngineNoticeRules.IsProgress(L.HoldingKeys(keys: "Ctrl")).ShouldBeTrue();
        EngineNoticeRules
            .IsProgress(L.MacroRunning(name: "Informe", index: 1, total: 3))
            .ShouldBeTrue();
        EngineNoticeRules.IsProgress(L.Released).ShouldBeFalse();
        EngineNoticeRules.IsProgress(L.MacroCancelled(name: "Informe")).ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "AVI-002")]
    [Trait("Req", "EJE-004")]
    public void A_Mantener_under_a_finger_is_fixed_while_it_is_held()
    {
        EngineNoticeRules.HoldInProgress(EngineSnapshot.Empty).ShouldBeNull();

        // A latched toggle has no finger on it: it is not «holding».
        EngineNoticeRules.HoldInProgress(With(Item(contact: null, "Negrita"))).ShouldBeNull();

        EngineNoticeRules
            .HoldInProgress(With(Item(contact: null, "Negrita"), Item(contact: 7, "Ctrl")))
            .ShouldBe(L.HoldingKeys(keys: "Ctrl"));
        EngineNoticeRules.HoldInProgress(With(Item(contact: 7, null))).ShouldBe(L.Holding);
    }

    [Fact]
    [Trait("Req", "AVI-002")]
    [Trait("Req", "EJE-010")]
    public void A_running_macro_is_fixed_with_its_step_while_it_runs()
    {
        EngineNoticeRules.MacroInProgress(EngineSnapshot.Empty).ShouldBeNull();

        var running = EngineSnapshot.Empty with
        {
            Macro = new MacroRun(new MacroRunId(1), Shortcut, 1, 3, null) { Name = "Informe" },
        };
        EngineNoticeRules
            .MacroInProgress(running)
            .ShouldBe(L.MacroRunning(name: "Informe", index: 2, total: 3));

        // Past its last step it still says «3 de 3», never «4 de 3».
        var ending = running with
        {
            Macro = running.Macro with { StepIndex = 3 },
        };
        EngineNoticeRules
            .MacroInProgress(ending)
            .ShouldBe(L.MacroRunning(name: "Informe", index: 3, total: 3));
    }

    private static EngineSnapshot With(params PressedItem[] held) =>
        EngineSnapshot.Empty with
        {
            Held = new ValueList<PressedItem>([.. held]),
            Version = 1,
        };

    private static PressedItem Item(int? contact, string? label) =>
        new(
            contact is { } id ? HolderId.ForContact(id) : HolderId.ForContact(99),
            HoldOrigin.Contact,
            Shortcut,
            contact,
            [],
            MouseButtons.None,
            0,
            null
        )
        {
            Label = label,
        };
}
