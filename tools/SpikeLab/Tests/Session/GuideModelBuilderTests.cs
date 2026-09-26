using Clicalo.Tools.SpikeLab.Scripting;
using Clicalo.Tools.SpikeLab.Session;
using Clicalo.Tools.SpikeLab.Tests.Scripting;
using Microsoft.Extensions.Time.Testing;

namespace Clicalo.Tools.SpikeLab.Tests.Session;

/// <summary>The guide strip shows the step, the repetitions and the automatic measurements, and red never alone.</summary>
public sealed class GuideModelBuilderTests
{
    private readonly FakeTimeProvider _time = new();

    [Fact]
    public void The_current_step_is_shown_with_its_progress_and_green_while_nothing_changed()
    {
        var engine = Engine(TestScripts.Tap("7"), TestScripts.Tap("8"));
        engine.RecordAutomatic(TestScripts.Clean);

        var model = GuideModelBuilder.Build(engine.Snapshot(), Status(), TestScripts.RestoreBudget);

        model.Heading.ShouldBe("S1 · paso 1 de 2 (fila 7)");
        model.Title.ShouldBe("Panel · dedo · Bloc de notas");
        model.Instruction.ShouldBe("Toca el panel.");
        model.Progress.ShouldBe(
            "Repeticiones: 1 de 20 · correctas: 1 · fallos: 0 · comprobación final: pendiente"
        );
        model.IsGreen.ShouldBeTrue();
        model.StatusWord.ShouldBe("Todo bien.");
        model.StepActionName.ShouldBeNull();
    }

    [Fact]
    public void The_measurements_show_foreground_activations_violations_command_lease_and_probe()
    {
        var engine = Engine(TestScripts.Tap("7"));
        var status = Status() with
        {
            Foreground = "notepad",
            SinceStep = new MeasurementCounters(2, 0, 0, 0, 1, 3, 0, 0, 0, 0),
            TotalViolations = 4,
            LastCommand = "Negrita por Invoke",
            LastLatencyMs = 12.34,
            LastLease = "TextInput concedida (paso 2)",
            LastRestore = "Restored",
            ProbeOpen = true,
            PiecesNotReady = 3,
        };

        var lines = GuideModelBuilder
            .Build(engine.Snapshot(), status, TestScripts.RestoreBudget)
            .Measurements;

        lines.ShouldBe([
            "Primer plano: notepad",
            "Cambios de primer plano: 2 · activaciones de superficies (WM_ACTIVATE): 0 · reg01.violations: 4",
            "Última orden: Negrita por Invoke · latencia: 12,3 ms",
            "Concesión: TextInput concedida (paso 2) · devolución: Restored",
            "La sonda recibió: F24 = 1, caracteres = 3, menú = 0",
            "Enviar teclas: no · piezas que no arrancaron: 3 (ver la ventana de control)",
        ]);
    }

    [Fact]
    public void An_activation_since_the_step_started_turns_it_red_with_the_reason_in_words()
    {
        var engine = Engine(TestScripts.Tap("7"));
        var status = Status() with
        {
            SinceStep = new MeasurementCounters(0, 0, 1, 0, 0, 0, 0, 0, 0, 0),
        };

        var model = GuideModelBuilder.Build(engine.Snapshot(), status, TestScripts.RestoreBudget);

        model.IsGreen.ShouldBeFalse();
        model.StatusWord.ShouldStartWith(
            "Algo cambió: Una superficie recibió 1 mensajes de activación"
        );
    }

    [Fact]
    public void A_failed_repetition_keeps_the_strip_red()
    {
        var engine = Engine(TestScripts.Manual("9a"));
        engine.MarkFailed(RepetitionEvidence.Empty);

        var model = GuideModelBuilder.Build(engine.Snapshot(), Status(), TestScripts.RestoreBudget);

        model.IsGreen.ShouldBeFalse();
        model.StatusWord.ShouldBe("Algo cambió: hay repeticiones fallidas.");
    }

    [Fact]
    public void The_forced_activation_row_expects_its_violation_and_offers_its_button()
    {
        var forced = SpikeScripts.S1.Steps.Single(step =>
            string.Equals(step.Id, "31", StringComparison.Ordinal)
        );
        var engine = Engine(forced);
        var status = Status() with
        {
            SinceStep = new MeasurementCounters(2, 1, 1, 1, 0, 0, 0, 0, 0, 0),
        };

        var model = GuideModelBuilder.Build(engine.Snapshot(), status, TestScripts.RestoreBudget);

        model.IsGreen.ShouldBeTrue();
        model.StepActionName.ShouldBe("Forzar activación del panel");
    }

    [Fact]
    public void All_repetitions_without_the_final_check_ask_for_it()
    {
        var engine = Engine(TestScripts.Tap("7", required: 1));
        engine.RecordAutomatic(TestScripts.Clean);

        GuideModelBuilder
            .Build(engine.Snapshot(), Status(), TestScripts.RestoreBudget)
            .StatusWord.ShouldBe("Todo bien. Haz la comprobación final y toca «Funcionó».");

        engine.MarkWorked(RepetitionEvidence.Empty);
        GuideModelBuilder
            .Build(engine.Snapshot(), Status(), TestScripts.RestoreBudget)
            .StatusWord.ShouldBe("Paso superado: toca «Siguiente».");
    }

    [Fact]
    public void The_end_of_the_script_shows_the_verdict()
    {
        var engine = Engine(TestScripts.Manual("1", required: 1));
        engine.MarkWorked(RepetitionEvidence.Empty);
        engine.Next();

        var model = GuideModelBuilder.Build(engine.Snapshot(), Status(), TestScripts.RestoreBudget);

        model.Heading.ShouldBe("S1 · prueba · guion terminado");
        model.StatusWord.ShouldBe("Guion terminado: superado");
        model.IsGreen.ShouldBeTrue();
    }

    private static LabStatus Status() => new() { Foreground = "notepad" };

    private ScriptEngine Engine(params ScriptStep[] steps) =>
        new(TestScripts.Script(steps), _time, TestScripts.RestoreBudget);
}
