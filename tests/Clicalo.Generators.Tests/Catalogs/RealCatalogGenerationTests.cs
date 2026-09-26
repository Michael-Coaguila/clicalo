using System.Collections;
using System.Text.Json;
using Clicalo.TestKit;

namespace Clicalo.Generators.Tests.Catalogs;

/// <summary>
/// The real catalogs of data/catalogs generate, with the hand-written Domain types, code that compiles without
/// warnings and holds the values of the files: the same guarantee the Clicalo.Domain build relies on.
/// </summary>
public sealed class RealCatalogGenerationTests
{
    private static readonly Lazy<GeneratorRun> Run = new(() =>
        CatalogGeneratorHarness.Run(CatalogGeneratorHarness.RealCatalogs())
    );

    [Fact]
    [Trait("Req", "CAT-001")]
    [Trait("Req", "NFR-020")]
    public void Real_catalogs_generate_the_Domain_core_without_diagnostics()
    {
        Run.Value.Diagnostics.ShouldBeEmpty();
        Run.Value.CompilationProblems.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "CAT-001")]
    public void Every_catalog_key_becomes_a_KeyIds_member_in_catalog_order()
    {
        using var keysFile = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RepoPaths.Data, "catalogs", "keys.json"))
        );
        var expected = keysFile
            .RootElement.GetProperty("keys")
            .EnumerateArray()
            .Select(k => k.GetProperty("id").GetString())
            .ToList();
        using var assembly = GeneratedAssembly.Load(Run.Value);

        var generated = ((IEnumerable)assembly.Static("Clicalo.Domain.Keys.KeyIds", "All")!)
            .Cast<object>()
            .Select(k => (string?)GeneratedAssembly.Property(k, "Value"))
            .ToList();

        generated.ShouldBe(expected);
        ((IEnumerable)assembly.Static("Clicalo.Domain.Keys.KeyDefinitions", "All")!)
            .Cast<object>()
            .Count()
            .ShouldBe(expected.Count);
    }

    [Fact]
    [Trait("Req", "NFR-020")]
    public void Generated_timings_match_the_documented_values()
    {
        using var assembly = GeneratedAssembly.Load(Run.Value);
        const string Timings = "Clicalo.Domain.Timing.Timings";

        assembly.Static(Timings + "+Touch", "LongPress").ShouldBe(TimeSpan.FromMilliseconds(600));
        assembly.Static(Timings + "+Dimming", "DimDelay").ShouldBe(TimeSpan.FromSeconds(2.5));
        assembly
            .Static(Timings + "+Confirmation", "DestructiveConfirmWindow")
            .ShouldBe(TimeSpan.FromSeconds(3.5));
        assembly
            .Static(Timings + "+Injection", "InterEventDelay")
            .ShouldBe(TimeSpan.FromMilliseconds(20));
        var crashLoop = assembly.Static(Timings + "+App", "CrashLoop")!;
        GeneratedAssembly.Property(crashLoop, "Count").ShouldBe(3);
        GeneratedAssembly.Property(crashLoop, "Window").ShouldBe(TimeSpan.FromMinutes(10));
        var restartLoop = assembly.Static(Timings + "+Guardian", "RestartLoop")!;
        GeneratedAssembly.Property(restartLoop, "Count").ShouldBe(3);
        GeneratedAssembly.Property(restartLoop, "Window").ShouldBe(TimeSpan.FromMinutes(10));
        assembly
            .Static(Timings + "+Backups", "PreviousVersionRetention")
            .ShouldBe(TimeSpan.FromDays(7));
        assembly
            .Static(Timings + "+Injection", "ClipboardCaptureMaxBytes")
            .ShouldBe(64L * 1024 * 1024);
    }

    [Fact]
    [Trait("Req", "PAN-002")]
    [Trait("Req", "CUA-007")]
    public void Generated_sizes_match_docs_04()
    {
        using var assembly = GeneratedAssembly.Load(Run.Value);
        const string Sizes = "Clicalo.Domain.Catalog.PanelSizes";
        var medium = assembly.Static(Sizes, "M")!;
        var layout = assembly.Static(Sizes, "Layout")!;

        GeneratedAssembly.Property(medium, "TileWidthPx").ShouldBe(92);
        GeneratedAssembly.Property(medium, "TileHeightPx").ShouldBe(78);
        GeneratedAssembly.Property(medium, "GapPx").ShouldBe(8);
        GeneratedAssembly.Property(medium, "StripCapacity").ShouldBe(8);
        GeneratedAssembly.Property(layout, "PanelMinWidthPx").ShouldBe(288);
        GeneratedAssembly.Property(layout, "MinTouchTargetPx").ShouldBe(44);
        GeneratedAssembly.Property(layout, "TextScaleTileGrowth").ShouldBe(1.6);

        // PAN-002: M with 3 columns is 316 px wide.
        var width = Math.Max(
            (int)GeneratedAssembly.Property(layout, "PanelMinWidthPx")!,
            (3 * (int)GeneratedAssembly.Property(medium, "TileWidthPx")!)
                + (2 * (int)GeneratedAssembly.Property(medium, "GapPx")!)
                + (int)GeneratedAssembly.Property(layout, "PanelChromeWidthPx")!
        );
        width.ShouldBe(316);
    }

    [Fact]
    [Trait("Req", "TAC-001")]
    public void Generated_presets_default_to_mild_tremor()
    {
        using var assembly = GeneratedAssembly.Load(Run.Value);

        var preset = assembly.Static("Clicalo.Domain.Catalog.TouchPresets", "Default")!;

        GeneratedAssembly.Property(preset, "Id").ShouldBe("mild-tremor");
        GeneratedAssembly.Property(preset, "Debounce").ShouldBe(TimeSpan.FromMilliseconds(300));
        GeneratedAssembly.Property(preset, "HitSlopPx").ShouldBe(14);
    }
}
