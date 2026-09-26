using Clicalo.Tools.SpikeLab.Scripting;

namespace Clicalo.Tools.SpikeLab.Tests.Scripting;

/// <summary>Small scripts and evidence for the engine tests.</summary>
internal static class TestScripts
{
    public static readonly TimeSpan RestoreBudget = TimeSpan.FromMilliseconds(200);

    public static ScriptStep Tap(string id, int required = 20) =>
        new(
            id,
            "Panel · dedo · Bloc de notas",
            "Toca el panel.",
            required,
            StepTrigger.SurfaceTap,
            EvidenceCheck.NonActivation
        )
        {
            Surface = SurfaceGroup.Panel,
        };

    public static ScriptStep Manual(string id, int required = 3) =>
        new(
            id,
            "Narrador · aviso",
            "Toca «Aviso cortés».",
            required,
            StepTrigger.Manual,
            EvidenceCheck.NoOwnForeground | EvidenceCheck.NoViolation
        );

    public static SpikeScript Script(params ScriptStep[] steps) =>
        new(SpikeId.S1, "S1 · prueba", "docs/testing/spikes/S1.md", [.. steps]);

    public static RepetitionEvidence Clean { get; } =
        new()
        {
            Trigger = new TriggerInfo(StepTrigger.SurfaceTap)
            {
                Surface = "Panel#0",
                Group = SurfaceGroup.Panel,
                Tile = "bold",
            },
            ForegroundProcess = "notepad",
            TargetProcess = "notepad",
            TargetInFront = true,
            LatencyMs = 12,
        };

    public static RepetitionEvidence Activated { get; } =
        Clean with
        {
            Delta = new MeasurementCounters(1, 1, 1, 0, 0, 0, 0, 0, 0, 0),
        };
}
