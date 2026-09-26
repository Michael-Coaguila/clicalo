using System;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Clicalo.Generators.Common;

/// <summary>Helpers shared by every Clícalo source generator.</summary>
internal static class GeneratorContext
{
    /// <summary>Reads the generator profile of the project being compiled.</summary>
    public static IncrementalValueProvider<GeneratorProfile> Profile(
        IncrementalGeneratorInitializationContext context
    ) =>
        context.AnalyzerConfigOptionsProvider.Select(
            static (options, _) => ParseProfile(options.GlobalOptions)
        );

    /// <summary>
    /// Selects the additional files whose repository-relative directory ends with <paramref name="directory"/>
    /// (for example <c>data/i18n</c>), independent of the path separator.
    /// </summary>
    public static IncrementalValuesProvider<AdditionalText> FilesIn(
        IncrementalGeneratorInitializationContext context,
        string directory
    )
    {
        var normalized = directory.Replace('\\', '/').TrimEnd('/');
        return context.AdditionalTextsProvider.Where(file =>
        {
            var dir = Path.GetDirectoryName(file.Path)?.Replace('\\', '/').TrimEnd('/');
            return dir is not null && dir.EndsWith(normalized, StringComparison.OrdinalIgnoreCase);
        });
    }

    /// <summary>Location pointing at a line and column of an additional file, for data diagnostics.</summary>
    public static Location At(AdditionalText file, int line, int column)
    {
        var position = new Microsoft.CodeAnalysis.Text.LinePosition(
            Math.Max(0, line - 1),
            Math.Max(0, column - 1)
        );
        return Location.Create(
            file.Path,
            default,
            new Microsoft.CodeAnalysis.Text.LinePositionSpan(position, position)
        );
    }

    private static GeneratorProfile ParseProfile(AnalyzerConfigOptions options) =>
        options.TryGetValue("build_property.ClicaloGeneratorProfile", out var value)
        && Enum.TryParse<GeneratorProfile>(value, ignoreCase: true, out var profile)
            ? profile
            : GeneratorProfile.None;
}
