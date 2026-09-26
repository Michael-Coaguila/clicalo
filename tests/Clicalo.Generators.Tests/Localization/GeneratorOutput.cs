using Microsoft.CodeAnalysis;

namespace Clicalo.Generators.Tests.Localization;

/// <summary>What one generator run produced.</summary>
internal sealed record GeneratorOutput(GeneratorDriverRunResult Result, Compilation Compilation)
{
    public IReadOnlyList<Diagnostic> Diagnostics => Result.Diagnostics;

    public IReadOnlyList<GeneratedSourceResult> Sources =>
        Result.Results.SelectMany(static r => r.GeneratedSources).ToList();

    /// <summary>Text of the generated file whose hint name ends with <paramref name="suffix"/>.</summary>
    public string Source(string suffix) =>
        Sources
            .Single(s => s.HintName.EndsWith(suffix, StringComparison.Ordinal))
            .SourceText.ToString();

    /// <summary>Compiler errors of the Domain message types plus the generated code.</summary>
    public IReadOnlyList<Diagnostic> CompilationErrors =>
        Compilation
            .GetDiagnostics()
            .Where(static d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

    /// <summary>The single diagnostic, as <c>id file(line,column)</c> with 1-based positions.</summary>
    public string Single()
    {
        Diagnostics.Count.ShouldBe(1, string.Join(Environment.NewLine, Diagnostics));
        return Describe(Diagnostics[0]);
    }

    public static string Describe(Diagnostic diagnostic)
    {
        if (diagnostic.Location == Location.None)
        {
            return diagnostic.Id;
        }

        var span = diagnostic.Location.GetLineSpan();
        return diagnostic.Id
            + " "
            + Path.GetFileName(span.Path)
            + "("
            + (span.StartLinePosition.Line + 1)
            + ","
            + (span.StartLinePosition.Character + 1)
            + ")";
    }
}
