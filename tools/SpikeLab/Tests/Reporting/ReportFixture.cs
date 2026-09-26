using Clicalo.Application.Foreground;
using Clicalo.Tools.SpikeLab.Composition;
using Clicalo.Tools.SpikeLab.Reporting;
using Clicalo.Tools.SpikeLab.Scripting;
using Clicalo.Tools.SpikeLab.Tests.Scripting;
using Microsoft.Extensions.Time.Testing;

namespace Clicalo.Tools.SpikeLab.Tests.Reporting;

/// <summary>A run with a passed step, a failed one, a discarded attempt and a lease, and its report context.</summary>
internal static class ReportFixture
{
    public static readonly DateTimeOffset Start = new(2026, 9, 26, 8, 15, 30, TimeSpan.Zero);

    public static ScriptSnapshot Run()
    {
        var time = new FakeTimeProvider(Start);
        var lease = new ScriptStep(
            "5",
            "Acceso por voz (UIA) · sonda",
            "Di «clic Buscar».",
            20,
            StepTrigger.LeaseCycle,
            EvidenceCheck.LeaseRoundTrip | EvidenceCheck.ProbeSilent
        )
        {
            Lease = LeaseKind.TextInput,
            Origin = LeaseOrigin.UiaInvoke,
        };
        var engine = new ScriptEngine(
            TestScripts.Script(TestScripts.Tap("1", required: 2), TestScripts.Tap("2"), lease),
            time,
            TestScripts.RestoreBudget
        );

        engine.RecordAutomatic(TestScripts.Clean);
        engine.RecordAutomatic(TestScripts.Clean with { LatencyMs = 30 });
        engine.MarkWorked(RepetitionEvidence.Empty);
        engine.Next();

        engine.RecordAutomatic(TestScripts.Clean);
        engine.Repeat();
        time.Advance(TimeSpan.FromSeconds(5));
        engine.RecordAutomatic(TestScripts.Activated);
        engine.Next();

        engine.RecordAutomatic(
            new RepetitionEvidence
            {
                Trigger = new TriggerInfo(StepTrigger.LeaseCycle) { Channel = "uia" },
                Delta = new MeasurementCounters(2, 0, 1, 0, 0, 0, 0, 3, 1, 0),
                Lease = new LeaseEvidence(LeaseKind.TextInput, LeaseOrigin.UiaInvoke)
                {
                    Granted = true,
                    LadderStep = 2,
                    AcquireMs = 41.234,
                    Restore = RestoreOutcome.Restored,
                    RestoreMs = 12,
                    PreviousProcess = "InputProbe",
                    PreviousWasProbe = true,
                    ForegroundReturned = true,
                },
                FieldCount = 1,
                FieldsWithText = 1,
            }
        );
        return engine.Snapshot();
    }

    public static ReportContext Context() =>
        new(
            new MachineInfo("Microsoft Windows NT 10.0.26200.0", "X64", "2.0.0-dev+abc123"),
            Start.AddMinutes(10),
            [
                new LabComponent(
                    "SurfaceRegistry",
                    LabComponentState.Ready,
                    "Registro de superficies."
                ),
                new LabComponent(
                    "ForegroundOrchestrator",
                    LabComponentState.Pending,
                    "Espera a ForegroundControl, ForegroundMonitor e InternalRightsHotkey."
                ),
            ],
            [new LabLogEntry(Start, "foreground", "Primer plano: notepad.")]
        )
        {
            SendsKeys = false,
            VoiceNumbers = true,
        };
}
