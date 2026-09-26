using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using Clicalo.Generators.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Clicalo.Generators.Localization;

/// <summary>
/// Generates <c>Clicalo.Domain.Messages</c> (<c>MessageKey</c>, <c>L</c>, <c>MessageCatalog</c>) from
/// <c>data/i18n</c> for projects with the <c>Domain</c> generator profile, and turns every i18n data error into a
/// CLCI compiler error at its exact position in the JSON file (blueprint §8.5, D18, ADR-0011).
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class LocalizationGenerator : IIncrementalGenerator
{
    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var files = GeneratorContext
            .FilesIn(context, LocalizationFiles.Directory)
            .Where(static file => LocalizationFiles.IsRelevant(Path.GetFileName(file.Path)))
            .Collect();
        var input = files.Combine(GeneratorContext.Profile(context));
        context.RegisterSourceOutput(
            input,
            static (spc, pair) => Execute(spc, pair.Left, pair.Right)
        );
    }

    private static void Execute(
        SourceProductionContext context,
        ImmutableArray<AdditionalText> files,
        GeneratorProfile profile
    )
    {
        if (profile != GeneratorProfile.Domain)
        {
            return;
        }

        var byPath = new Dictionary<string, AdditionalText>(StringComparer.Ordinal);
        var data = new List<LocalizationDataFile>();
        foreach (var file in files.OrderBy(static f => f.Path, StringComparer.Ordinal))
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            byPath[file.Path] = file;
            data.Add(
                new LocalizationDataFile(
                    file.Path,
                    file.GetText(context.CancellationToken)?.ToString()
                )
            );
        }

        var model = LocalizationModelBuilder.Analyze(data);
        foreach (var issue in model.Issues)
        {
            var location =
                issue.Path is not null && byPath.TryGetValue(issue.Path, out var file)
                    ? GeneratorContext.At(file, issue.Line, issue.Column)
                    : Location.None;
            context.ReportDiagnostic(
                Diagnostic.Create(LocalizationDiagnostics.For(issue.Id), location, issue.Message)
            );
        }

        foreach (var source in LocalizationEmitter.Emit(model))
        {
            context.AddSource(source.HintName, SourceText.From(source.Text, Encoding.UTF8));
        }
    }
}
