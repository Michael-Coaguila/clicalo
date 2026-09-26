using Clicalo.Tools.SpikeLab.Composition;
using Clicalo.Tools.SpikeLab.Reporting;

namespace Clicalo.Tools.SpikeLab.Tests.Reporting;

/// <summary>The Markdown summary says the verdict first and lists every failure, so Narrador reads it in order.</summary>
public sealed class MarkdownSummaryTests
{
    private static readonly string Summary = MarkdownSummary.Render(
        ReportFixture.Run(),
        ReportFixture.Context()
    );

    private static readonly string[] Lines = Summary.Split('\n');

    [Fact]
    public void The_title_and_the_verdict_come_first()
    {
        Lines[0].ShouldBe("# Informe de SpikeLab · S1 · prueba");
        Lines[2]
            .ShouldBe(
                "**Resultado: fallido.** 1 de 3 pasos superados, 1 fallidos, 0 no aplicables y 1 sin terminar."
            );
    }

    [Fact]
    public void The_settings_and_the_pieces_that_are_not_ready_are_listed()
    {
        Summary.ShouldContain("- Envío de teclas: desactivado · Números de voz: activados");
        Summary.ShouldContain("1 de 2 piezas no están listas");
        Summary.ShouldContain(
            "- Pendiente: ForegroundOrchestrator. Espera a ForegroundControl, ForegroundMonitor e InternalRightsHotkey."
        );
    }

    [Fact]
    public void Every_step_has_one_row_with_its_result_and_latency()
    {
        Summary.ShouldContain(
            "| Fila | Paso | Resultado | Correctas | Fallos | Latencia p95 (ms) |"
        );
        Summary.ShouldContain(
            "| 1 | Panel · dedo · Bloc de notas | Superado (2 de 2) | 2 | 0 | 30 |"
        );
        Summary.ShouldContain(
            "| 2 | Panel · dedo · Bloc de notas | Fallido (0 de 20) | 0 | 1 | 12 |"
        );
        Summary.ShouldContain(
            "| 5 | Acceso por voz (UIA) · sonda | En curso (1 de 20) | 1 | 0 | — |"
        );
    }

    [Fact]
    public void Failures_and_discarded_attempts_are_explained()
    {
        Summary.ShouldContain("### Fila 2 · Panel · dedo · Bloc de notas");
        Summary.ShouldContain("- Repetición 1 (");
        Summary.ShouldContain("Una superficie recibió 1 mensajes de activación");
        Summary.ShouldContain("- Intento descartado con «Repetir» (");
        Summary.ShouldContain("1 repeticiones, 0 fallidas.");
    }

    [Fact]
    public void Table_cells_escape_pipes()
    {
        var context = ReportFixture.Context() with
        {
            Components = [new LabComponent("Tray", LabComponentState.Failed, "a | b")],
        };

        MarkdownSummary.Render(ReportFixture.Run(), context).ShouldContain("a \\| b");
    }
}
