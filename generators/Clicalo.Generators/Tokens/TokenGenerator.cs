using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using Clicalo.Generators.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Clicalo.Generators.Tokens;

/// <summary>
/// Generates <c>Clicalo.UI.Wpf.Theming.Generated</c> from <c>data/tokens</c> for projects with the <c>UiWpf</c>
/// generator profile (blueprint §8.4). Colors are converted OKLCH → OKLab → linear sRGB → sRGB with CSS Color 4
/// gamut mapping, and every contrast pair is measured on the real composite: data errors, gamut losses and
/// contrast failures become CLCT compiler errors at their exact position in the JSON files.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class TokenGenerator : IIncrementalGenerator
{
    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var files = GeneratorContext
            .FilesIn(context, TokenFiles.Directory)
            .Where(static file => TokenFiles.IsRelevant(Path.GetFileName(file.Path)))
            .Collect();
        var input = files
            .Combine(GeneratorContext.Profile(context))
            .WithTrackingName(TrackingNames.Input);
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
        if (profile != GeneratorProfile.UiWpf)
        {
            return;
        }

        var byPath = new Dictionary<string, AdditionalText>(StringComparer.Ordinal);
        var sources = new List<TokenSourceFile>();
        foreach (var file in files.OrderBy(static f => f.Path, StringComparer.Ordinal))
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            byPath[file.Path] = file;
            sources.Add(
                new TokenSourceFile(file.Path, file.GetText(context.CancellationToken)?.ToString())
            );
        }

        var model = TokenModelBuilder.Build(sources);
        foreach (var issue in model.Issues)
        {
            var location =
                issue.Path is not null && byPath.TryGetValue(issue.Path, out var file)
                    ? GeneratorContext.At(file, issue.Line, issue.Column)
                    : Location.None;
            context.ReportDiagnostic(
                Diagnostic.Create(TokenDiagnostics.For(issue.Id), location, issue.Message)
            );
        }

        if (!model.CanEmit)
        {
            return;
        }

        foreach (var source in TokenEmitter.Emit(model))
        {
            context.AddSource(source.HintName, SourceText.From(source.Text, Encoding.UTF8));
        }
    }

    /// <summary>Names of the pipeline steps, observable by incremental-generation tests.</summary>
    internal static class TrackingNames
    {
        /// <summary>The data files combined with the generator profile: the only input of the output.</summary>
        public const string Input = "Tokens.Input";
    }
}
