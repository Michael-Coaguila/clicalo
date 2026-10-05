using Clicalo.Application.Foreground;
using Clicalo.Tools.SpikeLab.Scripting;

namespace Clicalo.Tools.SpikeLab.Tests.Scripting;

public sealed class EvidenceEvaluatorTests
{
    [Fact]
    public void Clean_evidence_passes_every_non_activation_check() =>
        Evaluate(EvidenceCheck.NonActivation, TestScripts.Clean).ShouldBeEmpty();

    [Fact]
    public void Each_non_activation_check_reports_its_own_problem()
    {
        var evidence = TestScripts.Clean with
        {
            Delta = new MeasurementCounters(2, 1, 3, 1, 0, 0, 0, 0, 0, 0),
            TargetInFront = false,
            ForegroundProcess = "SpikeLab · Panel#0",
        };

        var problems = Evaluate(EvidenceCheck.NonActivation, evidence);

        problems.Length.ShouldBe(4);
        problems[0].ShouldContain("3 mensajes de activación");
        problems[1].ShouldContain("1 veces a una ventana de SpikeLab");
        problems[2].ShouldContain("reg01.violations subió en 1");
        problems[3].ShouldContain("dejó de estar delante");
    }

    [Fact]
    public void Checks_that_the_step_does_not_ask_for_are_not_applied() =>
        Evaluate(EvidenceCheck.None, TestScripts.Activated).ShouldBeEmpty();

    [Fact]
    public void An_unknown_target_is_not_a_failure() =>
        Evaluate(EvidenceCheck.TargetStillInFront, TestScripts.Clean with { TargetInFront = null })
            .ShouldBeEmpty();

    [Theory]
    [InlineData(1L, 150.0, true, 0)]
    [InlineData(0L, 150.0, true, 1)]
    [InlineData(2L, 150.0, true, 0)]
    [InlineData(1L, 250.0, true, 1)]
    [InlineData(1L, null, true, 1)]
    [InlineData(1L, 150.0, false, 1)]
    public void A_forced_activation_must_be_counted_come_back_in_budget_and_keep_the_style(
        long violations,
        double? restoredWithin,
        bool styleKept,
        int problems
    )
    {
        var evidence = TestScripts.Clean with
        {
            Delta = new MeasurementCounters(2, 1, 1, violations, 0, 0, 0, 0, 0, 0),
            RestoredWithinMs = restoredWithin,
            NoActivateStyleKept = styleKept,
        };

        Evaluate(EvidenceCheck.ForcedActivationReverted, evidence).Length.ShouldBe(problems);
    }

    [Fact]
    public void A_lease_round_trip_needs_a_grant_and_a_verified_return()
    {
        var granted = new LeaseEvidence(LeaseKind.TextInput, LeaseOrigin.Touch)
        {
            Granted = true,
            Restore = RestoreOutcome.RestoredAfterRetry,
            ForegroundReturned = true,
        };

        Evaluate(EvidenceCheck.LeaseRoundTrip, TestScripts.Clean with { Lease = granted })
            .ShouldBeEmpty();
        Evaluate(
                EvidenceCheck.LeaseRoundTrip,
                TestScripts.Clean with
                {
                    Lease = granted with { Restore = RestoreOutcome.Failed },
                }
            )
            .ShouldHaveSingleItem()
            .ShouldContain("Failed");
        Evaluate(
                EvidenceCheck.LeaseRoundTrip,
                TestScripts.Clean with
                {
                    Lease = granted with { ForegroundReturned = false },
                }
            )
            .ShouldHaveSingleItem()
            .ShouldContain("no está la ventana de antes");
    }

    [Fact]
    public void A_denied_or_unavailable_lease_says_why()
    {
        var denied = new LeaseEvidence(LeaseKind.TextInput, LeaseOrigin.UiaInvoke)
        {
            Denial = ForegroundDenialReason.RightsRefused,
        };
        var unavailable = denied with
        {
            Denial = null,
            Unavailable = "ForegroundOrchestrator pendiente.",
        };

        Evaluate(EvidenceCheck.LeaseGranted, TestScripts.Clean with { Lease = denied })
            .ShouldHaveSingleItem()
            .ShouldContain("RightsRefused");
        Evaluate(EvidenceCheck.LeaseGranted, TestScripts.Clean with { Lease = unavailable })
            .ShouldHaveSingleItem()
            .ShouldContain("pendiente");
        Evaluate(EvidenceCheck.LeaseGranted, TestScripts.Clean).ShouldHaveSingleItem();
    }

    [Fact]
    public void Text_must_reach_every_field_of_a_granted_lease()
    {
        var lease = new LeaseEvidence(LeaseKind.ControlCenter, LeaseOrigin.Touch)
        {
            Granted = true,
        };

        Evaluate(
                EvidenceCheck.TextReachedField,
                TestScripts.Clean with
                {
                    Lease = lease,
                    FieldCount = 3,
                    FieldsWithText = 2,
                }
            )
            .ShouldHaveSingleItem()
            .ShouldContain("2 de 3 campos");
        Evaluate(
                EvidenceCheck.TextReachedField,
                TestScripts.Clean with
                {
                    Lease = lease,
                    FieldCount = 3,
                    FieldsWithText = 3,
                }
            )
            .ShouldBeEmpty();
    }

    [Fact]
    public void The_probe_must_stay_silent_but_modifier_messages_are_only_recorded()
    {
        var modifiersOnly = TestScripts.Clean with
        {
            Delta = new MeasurementCounters(0, 0, 0, 0, 0, 0, 0, 6, 1, 0),
        };
        var f24 = TestScripts.Clean with
        {
            Delta = new MeasurementCounters(0, 0, 0, 0, 1, 2, 1, 6, 1, 0),
        };

        Evaluate(EvidenceCheck.ProbeSilent, modifiersOnly).ShouldBeEmpty();
        Evaluate(EvidenceCheck.ProbeSilent, f24)
            .ShouldHaveSingleItem()
            .ShouldBe("La sonda recibió F24 = 1, caracteres = 2, menú = 1.");
    }

    [Fact]
    public void The_probe_must_be_the_target_of_the_voice_origin_cycles()
    {
        var lease = new LeaseEvidence(LeaseKind.TextInput, LeaseOrigin.UiaInvoke)
        {
            PreviousProcess = "WINWORD",
        };

        Evaluate(EvidenceCheck.ProbeWasTarget, TestScripts.Clean with { Lease = lease })
            .ShouldHaveSingleItem()
            .ShouldContain("WINWORD");
        Evaluate(
                EvidenceCheck.ProbeWasTarget,
                TestScripts.Clean with
                {
                    Lease = lease with { PreviousWasProbe = true },
                }
            )
            .ShouldBeEmpty();
    }

    [Fact]
    public void A_voice_number_is_required_where_the_row_uses_Clicalo_numbers()
    {
        var withNumber = TestScripts.Clean with
        {
            Trigger = new TriggerInfo(StepTrigger.UiaCommand) { VoiceNumber = 7 },
        };

        Evaluate(EvidenceCheck.VoiceNumberInName, TestScripts.Clean).ShouldHaveSingleItem();
        Evaluate(EvidenceCheck.VoiceNumberInName, withNumber).ShouldBeEmpty();
    }

    private static System.Collections.Immutable.ImmutableArray<string> Evaluate(
        EvidenceCheck checks,
        RepetitionEvidence evidence
    ) => EvidenceEvaluator.Evaluate(checks, evidence, TestScripts.RestoreBudget);
}
