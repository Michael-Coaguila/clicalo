using System;
using System.Collections.Immutable;
using Clicalo.Generators.Common;
using Microsoft.CodeAnalysis;

namespace Clicalo.Generators.Catalogs;

/// <summary>
/// Generates the small typed core of the catalogs into Clicalo.Domain (D17): key identities and definitions
/// (keys.json, checked against keys.win32.json), named times and thresholds (timings.json), panel sizes
/// (sizes.json) and touch presets (touch-presets.json). Templates, library and seed stay data loaded at run time.
/// Only projects with <c>ClicaloGeneratorProfile=Domain</c> receive the code.
/// </summary>
[Generator(LanguageNames.CSharp)]
internal sealed class CatalogGenerator : IIncrementalGenerator
{
    /// <summary>Repository-relative folder of the catalogs.</summary>
    public const string CatalogDirectory = "data/catalogs";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var enabled = GeneratorContext
            .Profile(context)
            .Select(static (profile, _) => profile == GeneratorProfile.Domain)
            .WithTrackingName(TrackingNames.Profile);

        var files = GeneratorContext
            .FilesIn(context, CatalogDirectory)
            .Select(static (file, cancellationToken) => CatalogFile.From(file, cancellationToken))
            .Collect()
            .Select(static (files, _) => Sort(files))
            .WithTrackingName(TrackingNames.Files);

        var catalogs = files.Combine(enabled);

        Register(
            context,
            catalogs,
            KeysEmitter.FileName,
            KeysEmitter.Win32FileName,
            TrackingNames.Keys,
            KeysEmitter.Execute
        );
        Register(
            context,
            catalogs,
            TimingsEmitter.FileName,
            companion: null,
            TrackingNames.Timings,
            TimingsEmitter.Execute
        );
        Register(
            context,
            catalogs,
            SizesEmitter.FileName,
            companion: null,
            TrackingNames.Sizes,
            SizesEmitter.Execute
        );
        Register(
            context,
            catalogs,
            TouchPresetsEmitter.FileName,
            companion: null,
            TrackingNames.TouchPresets,
            TouchPresetsEmitter.Execute
        );
    }

    private static void Register(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<(EquatableArray<CatalogFile> Files, bool Enabled)> catalogs,
        string primary,
        string? companion,
        string trackingName,
        Action<SourceProductionContext, CatalogInput> execute
    )
    {
        var input = catalogs
            .Select(
                (pair, _) =>
                    new CatalogInput(
                        pair.Enabled,
                        primary,
                        Find(pair.Files, primary),
                        companion is null ? null : Find(pair.Files, companion)
                    )
            )
            .WithTrackingName(trackingName);
        context.RegisterSourceOutput(input, execute);
    }

    private static EquatableArray<CatalogFile> Sort(ImmutableArray<CatalogFile?> files)
    {
        var builder = ImmutableArray.CreateBuilder<CatalogFile>(files.Length);
        foreach (var file in files)
        {
            if (file is not null)
            {
                builder.Add(file);
            }
        }

        builder.Sort(static (left, right) => string.CompareOrdinal(left.Path, right.Path));
        return new EquatableArray<CatalogFile>(builder.ToImmutable());
    }

    private static CatalogFile? Find(EquatableArray<CatalogFile> files, string fileName)
    {
        foreach (var file in files)
        {
            if (string.Equals(file.FileName, fileName, StringComparison.OrdinalIgnoreCase))
            {
                return file;
            }
        }

        return null;
    }

    /// <summary>Names of the pipeline steps, observable by incremental-generation tests.</summary>
    internal static class TrackingNames
    {
        public const string Profile = "Catalogs.Profile";
        public const string Files = "Catalogs.Files";
        public const string Keys = "Catalogs.Keys";
        public const string Timings = "Catalogs.Timings";
        public const string Sizes = "Catalogs.Sizes";
        public const string TouchPresets = "Catalogs.TouchPresets";
    }
}
