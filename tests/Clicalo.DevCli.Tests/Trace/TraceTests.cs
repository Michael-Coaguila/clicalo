using Clicalo.DevCli.Trace;
using Clicalo.TestKit;

namespace Clicalo.DevCli.Tests.Trace;

/// <summary>
/// <c>cl trace</c>: the catalog reader, the trait scanner (which reads source text, so the samples here are written
/// with escaped quotes and never look like a real trait of this file), the report and the verb.
/// </summary>
public sealed class TraceTests
{
    private const string Catalog =
        "# Catálogo\n"
        + "- **REG-01 · MUST · El panel nunca quita el foco.** Texto.\n"
        + "- **EJE-003 · MUST · Orden de pulsación.** Texto. **Acepta:** algo.\n"
        + "- **EJE-004 · MUST · Sin prueba.** Texto.\n"
        + "- **ACC-005 · MUST · Narrador** (lee todo):\n"
        + "- **PAN-005 · SHOULD · Mover sin arrastrar.** **Aplazado a después de la 2.0** (D8).\n"
        + "- **PAN-006 · SHOULD · Otra cosa.** Texto.\n"
        + "- **MIG-001 · Retirado · Localizar la v1.** (D1).\n"
        + "| **DIS-34** | no es un requisito |\n"
        + "- **EC-EJE-10.** Un caso límite con su comportamiento esperado.\n";

    private const string Trait = "[Trait(\"Req\", \"";

    private static readonly string Source =
        "namespace Sample;\n"
        + "\n"
        + "/// <summary>Notation: "
        + Trait
        + "ZZZ-999\")].</summary>\n"
        + Trait
        + "REG-01\")]\n"
        + "public sealed class SampleTests\n"
        + "{\n"
        + "    private sealed class Helper\n"
        + "    {\n"
        + "        public void Call() { }\n"
        + "    }\n"
        + "\n"
        + "    [Fact]\n"
        + "    "
        + Trait
        + "EJE-003\")]\n"
        + "    "
        + Trait
        + "EC-EJE-10\")]\n"
        + "    public async Task Ctrl_A_is_pressed_in_order()\n"
        + "    {\n"
        + "    }\n"
        + "\n"
        + "    [Theory]\n"
        + "    [InlineData(\n"
        + "        1\n"
        + "    )]\n"
        + "    "
        + Trait
        + "EJE-999\")]\n"
        + "    public void A_typo(int value) => value.ShouldBe(1);\n"
        + "}\n";

    [Fact]
    public void The_catalog_gives_requirements_rules_and_edge_cases_in_order()
    {
        var entries = CatalogReader.Read(Catalog);

        entries
            .Select(static e => e.Id)
            .ShouldBe([
                "REG-01",
                "EJE-003",
                "EJE-004",
                "ACC-005",
                "PAN-005",
                "PAN-006",
                "MIG-001",
                "EC-EJE-10",
            ]);
        entries[1]
            .ShouldBe(
                new CatalogEntry(
                    "EJE-003",
                    RequirementPriority.Must,
                    "Orden de pulsación",
                    false,
                    3
                )
            );
        entries[3].Title.ShouldBe("Narrador", "a title that the bold closes without a period");
        entries[4].Deferred.ShouldBeTrue();
        entries[4].Priority.ShouldBe(RequirementPriority.Should);
        entries[6].Priority.ShouldBe(RequirementPriority.Retired);
        entries[7].Priority.ShouldBe(RequirementPriority.EdgeCase);
        entries[7].Module.ShouldBe("EC");
        entries[1].Module.ShouldBe("EJE");
    }

    [Fact]
    public void Traits_belong_to_their_test_or_to_the_whole_class_and_comments_are_skipped()
    {
        var references = TraitScanner.Scan("tests/Sample/SampleTests.cs", Source);

        references
            .Select(static r => (r.Id, r.Test))
            .ShouldBe([
                ("REG-01", "SampleTests"),
                ("EJE-003", "SampleTests.Ctrl_A_is_pressed_in_order"),
                ("EC-EJE-10", "SampleTests.Ctrl_A_is_pressed_in_order"),
                ("EJE-999", "SampleTests.A_typo"),
            ]);
        references[1].Line.ShouldBe(13);
        references[1].File.ShouldBe("tests/Sample/SampleTests.cs");
    }

    [Fact]
    public void The_report_marks_the_MUST_without_a_test_and_the_ones_only_in_the_manual_script()
    {
        var entries = CatalogReader.Read(Catalog);
        var references = TraitScanner.Scan("tests/Sample/SampleTests.cs", Source);

        var (markdown, summary) = TraceReport.Render(
            entries,
            references,
            new HashSet<string>(StringComparer.Ordinal) { "ACC-005" }
        );

        summary.ShouldBe(
            new TraceSummary(
                Requirements: 7,
                Must: 4,
                MustTested: 2,
                MustManualOnly: 1,
                MustUncovered: 1,
                UnknownTraits: 1
            )
        );
        markdown.ShouldContain("- " + TraceReport.UncoveredLabel + ": 1.\n");
        markdown.ShouldContain("- **EJE-004** · Sin prueba: SIN PRUEBA.\n");
        markdown.ShouldContain("- **ACC-005** · Narrador: solo en el guion manual.\n");
        markdown.ShouldContain("- **PAN-006** · SHOULD · Otra cosa.\n");
        markdown.ShouldContain("- «EJE-999» en `tests/Sample/SampleTests.cs`, línea 23 (");
        markdown.ShouldContain(
            "- **EJE-003** · MUST · Orden de pulsación: 1 prueba.\n"
                + "  - `tests/Sample/SampleTests.cs` · `SampleTests.Ctrl_A_is_pressed_in_order`\n"
        );
        markdown.ShouldContain(
            "- **PAN-005** · SHOULD · Mover sin arrastrar: aplazado, sin prueba.\n"
        );
        markdown.ShouldContain("- **MIG-001** · Retirado · Localizar la v1: retirado.\n");
        markdown.ShouldContain("### EC\n\n- **EC-EJE-10** · Un caso límite");
        markdown.ShouldNotContain("\r");
        markdown.ShouldNotContain("- **PAN-005** · SHOULD · Mover sin arrastrar.\n");
    }

    [Fact]
    public void The_report_is_the_same_whatever_the_order_the_sources_are_read_in()
    {
        var entries = CatalogReader.Read(Catalog);
        var first = TraitScanner.Scan("tests/A/ATests.cs", Source);
        var second = TraitScanner.Scan("tests/B/BTests.cs", Source);
        HashSet<string> manual = [];

        var forward = TraceReport.Render(entries, [.. first, .. second], manual).Markdown;
        var backward = TraceReport.Render(entries, [.. second, .. first], manual).Markdown;

        backward.ShouldBe(forward);
    }

    [Fact]
    public void The_verb_writes_the_report_and_fails_only_for_a_trait_that_names_nothing()
    {
        using var repository = new TemporaryRepository();
        repository.Write(TraceCommand.CatalogPath, Catalog);
        repository.Write(
            TraceCommand.ManualScriptPath,
            "# Guion\n\n- Narrador lee todo (ACC-005).\n"
        );
        var source = repository.Write("tests/Sample/SampleTests.cs", Source);
        repository.Write("tests/Sample/obj/Generated.cs", Source);

        var (code, output) = Run(repository.Root);

        code.ShouldBe(ExitCodes.Failure, output);
        output.ShouldContain(
            "tests/Sample/SampleTests.cs(23): error CLCT010: 'EJE-999' is not a requirement"
        );
        LastLine(output)
            .ShouldBe(
                "trace: 7 requirements; 2 of 4 MUST with a test, 1 only in the manual script, 1 without any; "
                    + "1 unknown traits; report in artifacts/cl/trace.md."
            );
        var report = Path.Combine(repository.Root, "artifacts", "cl", "trace.md");
        File.ReadAllText(report).ShouldStartWith("# Trazabilidad de requisitos\n");

        File.WriteAllText(source, Source.Replace("EJE-999", "EJE-004", StringComparison.Ordinal));
        var (fixedCode, fixedOutput) = Run(repository.Root);

        fixedCode.ShouldBe(ExitCodes.Success, fixedOutput);
        LastLine(fixedOutput)
            .ShouldContain("3 of 4 MUST with a test, 1 only in the manual script, 0 without any");
    }

    [Fact]
    public void Without_a_catalog_it_fails_in_one_line()
    {
        using var repository = new TemporaryRepository();

        var (code, output) = Run(repository.Root);

        code.ShouldBe(ExitCodes.Failure);
        LastLine(output).ShouldBe("trace: the catalog could not be read.");
    }

    [Fact]
    public void Every_requirement_trait_of_the_repository_names_a_requirement_of_the_catalog()
    {
        var entries = CatalogReader.Read(
            File.ReadAllText(Path.Combine(RepoPaths.Root, TraceCommand.CatalogPath))
        );

        var (markdown, summary, unknown) = TraceCommand.Analyze(RepoPaths.Root, entries);

        unknown.ShouldBeEmpty();
        summary.Requirements.ShouldBeGreaterThan(300);
        summary.MustTested.ShouldBeGreaterThan(200);
        markdown.ShouldContain("- **EJE-003** · MUST · ");
        markdown.ShouldContain(
            "KeyboardInjectionTests.Ctrl_A_is_pressed_in_order_and_released_in_reverse_order"
        );
    }

    private static (int Code, string Output) Run(string root)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = Cli.Run(["trace", "--repo", root], root, output, error);
        return (code, output.ToString() + error);
    }

    private static string LastLine(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[
            ^1
        ];
}
