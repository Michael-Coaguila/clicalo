using System.Collections;
using Clicalo.Generators.Catalogs;
using Microsoft.CodeAnalysis;

namespace Clicalo.Generators.Tests.Catalogs;

/// <summary>What the catalog generator emits from valid catalogs, and how it behaves as an incremental generator.</summary>
public sealed class CatalogGeneratorOutputTests
{
    private static readonly string[] GeneratedFiles =
    [
        "Clicalo.Domain.Keys.KeyGroup.g.cs",
        "Clicalo.Domain.Keys.KeyIds.g.cs",
        "Clicalo.Domain.Keys.KeyDefinitions.g.cs",
        "Clicalo.Domain.Timing.Timings.g.cs",
        "Clicalo.Domain.Catalog.PanelSize.g.cs",
        "Clicalo.Domain.Catalog.PanelSizes.g.cs",
        "Clicalo.Domain.Catalog.TouchPresets.g.cs",
    ];

    [Fact]
    public void Valid_catalogs_generate_code_that_compiles_without_warnings()
    {
        var run = CatalogGeneratorHarness.Run(SampleCatalogs.All());

        run.Diagnostics.ShouldBeEmpty();
        run.DriverDiagnostics.ShouldBeEmpty();
        run.Sources.Keys.ShouldBe(GeneratedFiles, ignoreOrder: true);
        run.CompilationProblems.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "NFR-020")]
    public void Every_timing_kind_becomes_a_typed_member()
    {
        var source = CatalogGeneratorHarness.Run(SampleCatalogs.All()).Sources[
            "Clicalo.Domain.Timing.Timings.g.cs"
        ];

        source.ShouldContain(
            "public static readonly global::System.TimeSpan LongPress = global::System.TimeSpan.FromTicks(6000000L);"
        );
        source.ShouldContain("public const int SwipeMinDistancePx = 60;");
        source.ShouldContain("public const double SwipeMaxSlope = 0.6d;");
        source.ShouldContain(
            "public static readonly global::Clicalo.Domain.Timing.CountWindow CrashLoop = new(3, global::System.TimeSpan.FromTicks(6000000000L));"
        );
        source.ShouldContain("public const long MessageMaxBytes = 16384L;");
        source.ShouldContain("public const int UndoDepth = 20;");
        source.ShouldContain(
            "public static readonly global::System.TimeSpan DimDelay = global::System.TimeSpan.FromTicks(25000000L);"
        );
        source.ShouldContain("/// <summary>Long press (600ms; NFR-020).</summary>");
    }

    [Fact]
    [Trait("Req", "NFR-020")]
    public void Generated_constants_hold_the_catalog_values()
    {
        using var assembly = GeneratedAssembly.Load(
            CatalogGeneratorHarness.Run(SampleCatalogs.All())
        );
        const string Timings = "Clicalo.Domain.Timing.Timings";

        assembly.Static(Timings + "+Touch", "LongPress").ShouldBe(TimeSpan.FromMilliseconds(600));
        assembly.Static(Timings + "+App", "DimDelay").ShouldBe(TimeSpan.FromSeconds(2.5));
        assembly.Static(Timings + "+Touch", "SwipeMaxSlope").ShouldBe(0.6);
        var crashLoop = assembly.Static(Timings + "+App", "CrashLoop")!;
        GeneratedAssembly.Property(crashLoop, "Count").ShouldBe(3);
        GeneratedAssembly.Property(crashLoop, "Window").ShouldBe(TimeSpan.FromMinutes(10));
        ((IEnumerable)assembly.Static(Timings + "+App", "RestartBackoff")!)
            .Cast<TimeSpan>()
            .ShouldBe([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30)]);
        var wait = assembly.Static(Timings + "+App", "WaitRange")!;
        GeneratedAssembly.Property(wait, "Min").ShouldBe(TimeSpan.FromMilliseconds(100));
        GeneratedAssembly.Property(wait, "Max").ShouldBe(TimeSpan.FromSeconds(10));
        GeneratedAssembly.Property(wait, "Step").ShouldBe(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    [Trait("Req", "EDI-009")]
    public void Keys_become_identities_with_group_modifier_and_side()
    {
        using var assembly = GeneratedAssembly.Load(
            CatalogGeneratorHarness.Run(SampleCatalogs.All())
        );
        const string KeyIds = "Clicalo.Domain.Keys.KeyIds";
        var ctrl = assembly.Static(KeyIds, "Ctrl")!;
        var leftCtrl = assembly.Static(KeyIds, "LeftCtrl")!;

        GeneratedAssembly.Property(ctrl, "Value").ShouldBe("ctrl");
        GeneratedAssembly
            .Property(assembly.Static(KeyIds, "NTilde")!, "IsCharacter")
            .ShouldBe(true);
        GeneratedAssembly.Property(assembly.Static(KeyIds, "A")!, "IsCharacter").ShouldBe(false);
        ((IEnumerable)assembly.Static(KeyIds, "All")!)
            .Cast<object>()
            .Select(k => GeneratedAssembly.Property(k, "Value"))
            .ShouldBe(["ctrl", "lctrl", "a", "char:ñ"]);

        var (found, arguments) = assembly.Invoke(
            "Clicalo.Domain.Keys.KeyDefinitions",
            "TryGet",
            leftCtrl,
            null
        );
        found.ShouldBe(true);
        var definition = arguments[1]!;
        GeneratedAssembly.Property(definition, "BaseKey").ShouldBe(ctrl);
        GeneratedAssembly.Property(definition, "Side")!.ToString().ShouldBe("Left");
        GeneratedAssembly.Property(definition, "Modifier")!.ToString().ShouldBe("Ctrl");
        GeneratedAssembly.Property(definition, "Group")!.ToString().ShouldBe("Sides");
        GeneratedAssembly.Property(definition, "IsModifier").ShouldBe(true);
    }

    [Fact]
    [Trait("Req", "TAC-001")]
    public void Touch_presets_become_immutable_records_with_a_default()
    {
        using var assembly = GeneratedAssembly.Load(
            CatalogGeneratorHarness.Run(SampleCatalogs.All())
        );
        const string Presets = "Clicalo.Domain.Catalog.TouchPresets";

        var standard = assembly.Static(Presets, "Standard")!;
        GeneratedAssembly.Property(standard, "Debounce").ShouldBe(TimeSpan.FromMilliseconds(150));
        GeneratedAssembly.Property(standard, "HitSlopPx").ShouldBe(8);
        GeneratedAssembly.Property(standard, "CancelMovePx").ShouldBe(45);
        GeneratedAssembly.Property(standard, "MinContact").ShouldBe(TimeSpan.Zero);
        GeneratedAssembly
            .Property(assembly.Static(Presets, "Default")!, "Id")
            .ShouldBe("mild-tremor");
        assembly.Invoke(Presets, "Find", "standard").Result.ShouldBe(standard);
        assembly.Invoke(Presets, "Find", "unknown").Result.ShouldBeNull();
    }

    [Fact]
    public void Output_is_deterministic()
    {
        var first = CatalogGeneratorHarness.Run(SampleCatalogs.All()).Sources;
        var second = CatalogGeneratorHarness.Run(SampleCatalogs.All()).Sources;

        second.ShouldBe(first);
        first.Values.ShouldAllBe(source => !source.Contains('\r', StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("UiWpf")]
    [InlineData("None")]
    [InlineData("")]
    public void Projects_without_the_Domain_profile_get_no_code_and_no_diagnostics(string profile)
    {
        var run = CatalogGeneratorHarness.Run(SampleCatalogs.All(), profile);

        run.Sources.ShouldBeEmpty();
        run.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Editing_one_catalog_leaves_the_other_outputs_cached()
    {
        var catalogs = SampleCatalogs.All();
        var first = CatalogGeneratorHarness.Run(
            CatalogGeneratorHarness.CreateDriver("Domain").AddAdditionalTexts([.. catalogs])
        );
        var timings = catalogs.Single(c =>
            string.Equals(Path.GetFileName(c.Path), "timings.json", StringComparison.Ordinal)
        );
        var edited = CatalogGeneratorHarness.Catalog(
            "timings.json",
            SampleCatalogs.Timings.Replace("\"600ms\"", "\"700ms\"", StringComparison.Ordinal)
        );

        var second = CatalogGeneratorHarness.Run(
            first.Driver.ReplaceAdditionalText(timings, edited)
        );

        var steps = second.Result.Results.Single().TrackedSteps;
        Reasons(steps[CatalogGenerator.TrackingNames.Keys])
            .ShouldAllBe(r =>
                r == IncrementalStepRunReason.Unchanged || r == IncrementalStepRunReason.Cached
            );
        Reasons(steps[CatalogGenerator.TrackingNames.Sizes])
            .ShouldAllBe(r =>
                r == IncrementalStepRunReason.Unchanged || r == IncrementalStepRunReason.Cached
            );
        Reasons(steps[CatalogGenerator.TrackingNames.Timings])
            .ShouldContain(IncrementalStepRunReason.Modified);
        second.Sources["Clicalo.Domain.Timing.Timings.g.cs"].ShouldContain("FromTicks(7000000L)");
    }

    private static IEnumerable<IncrementalStepRunReason> Reasons(
        IEnumerable<IncrementalGeneratorRunStep> steps
    ) => steps.SelectMany(step => step.Outputs).Select(output => output.Reason);
}
