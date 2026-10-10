using System.Globalization;
using System.Text;

namespace Clicalo.DevCli.Trace;

/// <summary>
/// The traceability report of <c>cl trace</c> (blueprint §10.6, deviations D-06): every requirement of the catalog with
/// the tests that name it, and first of all the MUST requirements nobody tests. It is Markdown with headings and lists
/// (no tables: Narrator reads it), in Spanish like the rest of the documentation, and it depends only on the catalog,
/// the test sources and the manual script: the same commit always gives the same bytes.
/// </summary>
internal static class TraceReport
{
    /// <summary>The line of the report the final line of <c>cl trace</c> is read from (build/BuildSteps).</summary>
    public const string UncoveredLabel = "MUST sin prueba automática ni guion manual";

    private const char NewLine = '\n';

    /// <summary>Renders the report.</summary>
    /// <param name="entries">The catalog, in its order.</param>
    /// <param name="references">Every requirement trait found in the test sources.</param>
    /// <param name="manual">The identifiers the manual acceptance script names.</param>
    public static (string Markdown, TraceSummary Summary) Render(
        IReadOnlyList<CatalogEntry> entries,
        IReadOnlyList<TestReference> references,
        IReadOnlySet<string> manual
    )
    {
        var known = entries.Select(static e => e.Id).ToHashSet(StringComparer.Ordinal);
        var byId = references
            .Where(r => known.Contains(r.Id))
            .GroupBy(static r => r.Id, StringComparer.Ordinal)
            .ToDictionary(
                static g => g.Key,
                static g =>
                    (IReadOnlyList<TestReference>)
                        [
                            .. g.OrderBy(static r => r.File, StringComparer.Ordinal)
                                .ThenBy(static r => r.Line),
                        ],
                StringComparer.Ordinal
            );
        var unknown = references
            .Where(r => !known.Contains(r.Id))
            .OrderBy(static r => r.File, StringComparer.Ordinal)
            .ThenBy(static r => r.Line)
            .ToList();

        var requirements = entries
            .Where(static e => e.Priority != RequirementPriority.EdgeCase)
            .ToList();
        var edgeCases = entries
            .Where(static e => e.Priority == RequirementPriority.EdgeCase)
            .ToList();
        var must = Active(requirements, RequirementPriority.Must);
        var optional = requirements
            .Where(static e =>
                !e.Deferred && e.Priority is RequirementPriority.Should or RequirementPriority.Could
            )
            .ToList();
        var mustUntested = must.Where(e => !byId.ContainsKey(e.Id)).ToList();
        var mustManual = mustUntested.Where(e => manual.Contains(e.Id)).ToList();
        var summary = new TraceSummary(
            requirements.Count,
            must.Count,
            must.Count - mustUntested.Count,
            mustManual.Count,
            mustUntested.Count - mustManual.Count,
            unknown.Count
        );

        var report = new StringBuilder();
        Line(report, "# Trazabilidad de requisitos");
        Line(report);
        Line(
            report,
            "Generado por `cl trace` con `docs/requirements/catalog.md`, los rasgos `Trait(\"Req\", \"ID\")` de las "
                + "pruebas y `docs/guides/aceptacion-manual.md`. No lo edites: vuelve a ejecutar `cl trace`."
        );
        Line(report);
        Line(report, "## Resumen");
        Line(report);
        Line(
            report,
            "- Requisitos del catálogo: "
                + Number(requirements.Count)
                + " ("
                + Number(Count(requirements, RequirementPriority.Must))
                + " MUST, "
                + Number(Count(requirements, RequirementPriority.Should))
                + " SHOULD, "
                + Number(Count(requirements, RequirementPriority.Could))
                + " COULD y "
                + Number(Count(requirements, RequirementPriority.Retired))
                + " retirados)."
        );
        Line(
            report,
            "- Aplazados a después de esta versión: "
                + Number(requirements.Count(static e => e.Deferred))
                + "."
        );
        Line(
            report,
            "- MUST con prueba automática: "
                + Number(summary.MustTested)
                + " de "
                + Number(summary.Must)
                + "."
        );
        Line(report, "- MUST solo en el guion manual: " + Number(summary.MustManualOnly) + ".");
        Line(report, "- " + UncoveredLabel + ": " + Number(summary.MustUncovered) + ".");
        Line(
            report,
            "- SHOULD y COULD con prueba automática: "
                + Number(optional.Count(e => byId.ContainsKey(e.Id)))
                + " de "
                + Number(optional.Count)
                + "."
        );
        Line(
            report,
            "- Casos límite con prueba automática: "
                + Number(edgeCases.Count(e => byId.ContainsKey(e.Id)))
                + " de "
                + Number(edgeCases.Count)
                + "."
        );
        Line(
            report,
            "- Rasgos que no nombran un requisito del catálogo: " + Number(unknown.Count) + "."
        );

        Section(report, "MUST sin prueba automática");
        if (mustUntested.Count == 0)
        {
            Line(report, "Ninguno.");
        }

        foreach (var entry in mustUntested)
        {
            Line(
                report,
                "- **"
                    + entry.Id
                    + "** · "
                    + entry.Title
                    + ": "
                    + (manual.Contains(entry.Id) ? "solo en el guion manual." : "SIN PRUEBA.")
            );
        }

        Section(report, "SHOULD y COULD sin prueba automática");
        var optionalUntested = optional.Where(e => !byId.ContainsKey(e.Id)).ToList();
        if (optionalUntested.Count == 0)
        {
            Line(report, "Ninguno.");
        }

        foreach (var entry in optionalUntested)
        {
            Line(
                report,
                "- **"
                    + entry.Id
                    + "** · "
                    + Word(entry.Priority)
                    + " · "
                    + entry.Title
                    + (manual.Contains(entry.Id) ? ": solo en el guion manual." : ".")
            );
        }

        if (unknown.Count > 0)
        {
            Section(report, "Rasgos que no nombran un requisito del catálogo");
            foreach (var reference in unknown)
            {
                Line(
                    report,
                    "- «"
                        + reference.Id
                        + "» en `"
                        + reference.File
                        + "`, línea "
                        + Number(reference.Line)
                        + " ("
                        + reference.Test
                        + ")."
                );
            }
        }

        Section(report, "Requisitos y sus pruebas");
        string? module = null;
        foreach (var entry in entries)
        {
            if (!string.Equals(entry.Module, module, StringComparison.Ordinal))
            {
                module = entry.Module;
                Line(report, "### " + module);
                Line(report);
            }

            var tests = byId.GetValueOrDefault(entry.Id) ?? [];
            Line(
                report,
                "- **"
                    + entry.Id
                    + "** · "
                    + (
                        entry.Priority == RequirementPriority.EdgeCase
                            ? ""
                            : Word(entry.Priority) + " · "
                    )
                    + entry.Title
                    + ": "
                    + State(entry, tests.Count, manual.Contains(entry.Id))
            );
            foreach (var test in tests)
            {
                Line(report, "  - `" + test.File + "` · `" + test.Test + "`");
            }
        }

        return (report.ToString(), summary);
    }

    private static List<CatalogEntry> Active(
        List<CatalogEntry> requirements,
        RequirementPriority priority
    ) => [.. requirements.Where(e => e.Priority == priority && !e.Deferred)];

    private static int Count(List<CatalogEntry> requirements, RequirementPriority priority) =>
        requirements.Count(e => e.Priority == priority);

    private static string State(CatalogEntry entry, int tests, bool manual)
    {
        var counted = tests switch
        {
            0 => manual ? "sin prueba automática, en el guion manual" : "sin prueba",
            1 => "1 prueba",
            _ => Number(tests) + " pruebas",
        };
        if (entry.Priority == RequirementPriority.Retired)
        {
            return tests == 0 ? "retirado." : "retirado, " + counted + ".";
        }

        return entry.Deferred ? "aplazado, " + counted + "." : counted + ".";
    }

    private static string Word(RequirementPriority priority) =>
        priority switch
        {
            RequirementPriority.Must => "MUST",
            RequirementPriority.Should => "SHOULD",
            RequirementPriority.Could => "COULD",
            RequirementPriority.Retired => "Retirado",
            _ => "Caso límite",
        };

    private static void Section(StringBuilder report, string title)
    {
        Line(report);
        Line(report, "## " + title);
        Line(report);
    }

    private static void Line(StringBuilder report, string text = "") =>
        report.Append(text).Append(NewLine);

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
