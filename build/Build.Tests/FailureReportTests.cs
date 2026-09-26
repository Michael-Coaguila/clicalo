using System.Globalization;

namespace Clicalo.Build.Tests;

public sealed class FailureReportTests
{
    private static readonly DateTimeOffset Now = new(
        2026,
        9,
        25,
        14,
        3,
        12,
        TimeSpan.FromHours(-5)
    );

    [Fact]
    public void Starts_with_the_same_sentence_as_the_final_line_and_lists_the_facts()
    {
        var failure = new StepFailure(
            "build",
            new FailureDetails
            {
                Summary = "La compilación terminó con errores.",
                Command = "dotnet build Clicalo.slnx -c Release",
                ExitCode = 1,
                Sections = [new ReportSection("Errores de compilación (1)", "1. a\n")],
                Hint = "Corrige los errores.",
            }
        );

        var report = FailureReport.Render("check", failure, TimeSpan.FromSeconds(43), Now);

        report.ShouldBe(
            """
            # cl check: falló en build

            La compilación terminó con errores.

            - Orden: `cl check`
            - Paso: build
            - Comando: `dotnet build Clicalo.slnx -c Release`
            - Código de salida: 1
            - Tiempo hasta el fallo: 43 s
            - Fecha: 2026-09-25 14:03:12 (hora local)

            ## Errores de compilación (1)

            1. a

            ## Qué hacer

            Corrige los errores.

            """.ReplaceLineEndings("\n")
        );
    }

    [Fact]
    public void Explains_documented_exit_codes()
    {
        var failure = new StepFailure(
            "test",
            new FailureDetails
            {
                Summary = "Hay pruebas que fallaron.",
                ExitCode = 2,
                ExitCodeMeaning = Messages.TestExitCodeMeaning(2),
            }
        );

        FailureReport
            .Render("test", failure, TimeSpan.Zero, Now)
            .ShouldContain("- Código de salida: 2 (al menos una prueba falló)\n");
    }

    [Fact]
    public void A_defect_of_cl_keeps_the_exception_for_the_issue()
    {
        var details = FailureDetails.FromException(new InvalidOperationException("boom"));

        details.Summary.ShouldBe(Messages.UnexpectedFailed);
        details.Sections.ShouldHaveSingleItem().Body.ShouldContain("boom");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(8)]
    [InlineData(13)]
    public void Every_documented_test_exit_code_has_a_meaning(int exitCode) =>
        string.Equals(
                Messages.TestExitCodeMeaning(exitCode),
                Messages.TestExitCodeMeaning(-1),
                StringComparison.Ordinal
            )
            .ShouldBeFalse(exitCode.ToString(CultureInfo.InvariantCulture));
}
