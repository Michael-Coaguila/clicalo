using System.Collections;
using System.Reflection;
using System.Runtime.Loader;
using Clicalo.Generators.Tokens;
using Microsoft.CodeAnalysis;

namespace Clicalo.Generators.Tests.Tokens;

/// <summary>The token generator end to end: profile gating, diagnostics locations, deterministic output, and the
/// behavior of the generated code (compiled and executed against WPF stand-ins).</summary>
public sealed class TokenGeneratorTests
{
    private const string Generated = TokenEmitter.Namespace;

    private static readonly string[] HintNames =
    [
        "CategoryToken.g.cs",
        "ColorToken.g.cs",
        "DesignShapes.g.cs",
        "Motion.g.cs",
        "SystemHighContrastPalette.g.cs",
        "ThemePalette.g.cs",
        "ThemePalettes.g.cs",
    ];

    [Fact]
    public void Emits_the_theming_sources_for_the_UiWpf_profile_without_diagnostics()
    {
        var result = TokenGeneratorHarness.Run(TokenTestData.Files);

        result.Diagnostics.ShouldBeEmpty();
        result
            .Results.ShouldHaveSingleItem()
            .GeneratedSources.Select(s => s.HintName)
            .Order(StringComparer.Ordinal)
            .ShouldBe(HintNames);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Domain")]
    [InlineData("None")]
    public void Emits_nothing_for_other_profiles(string? profile)
    {
        var result = TokenGeneratorHarness.Run(TokenTestData.Files, profile);

        result.Diagnostics.ShouldBeEmpty();
        result.GeneratedTrees.ShouldBeEmpty();
    }

    [Fact]
    public void Output_is_deterministic()
    {
        var first = TokenGeneratorHarness
            .Run(TokenTestData.Files)
            .GeneratedTrees.Select(tree => tree.ToString());
        var second = TokenGeneratorHarness
            .Run(TokenTestData.Files)
            .GeneratedTrees.Select(tree => tree.ToString());

        second.ShouldBe(first);
    }

    [Fact]
    public void Colors_are_emitted_as_argb_literals_with_their_source_value()
    {
        var palettes = Source(TokenGeneratorHarness.Run(TokenTestData.Files), "ThemePalettes.g.cs");

        palettes.ShouldContain("Argb(0xFF56D3DA), // accent: oklch(0.80 0.11 200)");
        palettes.ShouldContain("Argb(0xFF006885), // accent: oklch(0.474 0.11 220)");
        palettes.ShouldContain("Argb(0xFFFFE600), // accent: #FFE600");
        palettes.ShouldContain(
            "public static global::System.Collections.Generic.IReadOnlyList<ThemePalette> All { get; } = [Dark, Light, HighContrast];"
        );
    }

    [Fact]
    public void Diagnostics_point_at_the_json_file_line_and_column()
    {
        var files = TokenTestData.With(
            TokenFiles.ThemePalettes,
            "\"card\": \"oklch(0.27",
            "\"card\": \"oklch(1.27"
        );
        var (line, column) = TokenTestData.PositionOf(
            files[TokenFiles.ThemePalettes],
            "1.27 0.014 260"
        );

        var diagnostic = TokenGeneratorHarness.Run(files).Diagnostics.ShouldHaveSingleItem();

        diagnostic.Id.ShouldBe("CLCT001");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        var span = diagnostic.Location.GetLineSpan();
        span.Path.ShouldBe(TokenTestData.Directory + TokenFiles.ThemePalettes);
        span.StartLinePosition.Line.ShouldBe(line - 1);
        span.StartLinePosition.Character.ShouldBe(column - 1);
    }

    [Fact]
    public void Structural_errors_stop_the_emission()
    {
        var result = TokenGeneratorHarness.Run(TokenTestData.Without(TokenFiles.ContrastPairs));

        result.Diagnostics.ShouldHaveSingleItem().Id.ShouldBe("CLCT007");
        result.GeneratedTrees.ShouldBeEmpty();
    }

    [Fact]
    public void Generated_code_compiles_without_warnings()
    {
        var generated = TokenGeneratorHarness.Run(TokenTestData.Files).GeneratedTrees;

        var diagnostics = TokenGeneratorHarness
            .CompileWithWpfStubs(generated)
            .GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(d => d.Severity >= DiagnosticSeverity.Warning);

        diagnostics.Select(d => d.ToString()).ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "TEM-001")]
    public void Generated_palettes_expose_colors_frozen_brushes_and_the_list_of_themes()
    {
        using var generated = GeneratedAssembly.Load();

        var all = ((IEnumerable)generated.Static("ThemePalettes", "All")).Cast<object>().ToList();
        var dark = generated.Static("ThemePalettes", "Dark");
        var accent = generated.EnumValue("ColorToken", "Accent");

        all.Count.ShouldBe(3);
        all[0].ShouldBeSameAs(dark);
        Property(dark, "Key").ShouldBe("dark");
        Property(dark, "Accent").ToString().ShouldBe("#FF56D3DA");
        Invoke(dark, "GetColor", accent).ToString().ShouldBe("#FF56D3DA");
        var brush = Invoke(dark, "CreateBrush", accent);
        Property(brush, "IsFrozen").ShouldBe(true);
        Property(brush, "Color").ToString().ShouldBe("#FF56D3DA");
        Invoke(dark, "GetCategoryTint", generated.EnumValue("CategoryToken", "Nav"))
            .ToString()
            .ShouldBe("#FFB1C8FF");
        Property(
                Invoke(
                    dark,
                    "CreateCategoryWashBrush",
                    generated.EnumValue("CategoryToken", "Nav")
                ),
                "IsFrozen"
            )
            .ShouldBe(true);
        Should
            .Throw<TargetInvocationException>(() =>
                Invoke(dark, "GetColor", generated.EnumValue("ColorToken", 999))
            )
            .InnerException.ShouldBeOfType<ArgumentOutOfRangeException>();
    }

    [Fact]
    [Trait("Req", "TEM-001")]
    public void The_system_palette_reads_the_windows_contrast_colors()
    {
        using var generated = GeneratedAssembly.Load();

        var palette = generated.Invoke("SystemHighContrastPalette", "Capture");

        Property(palette, "Key").ShouldBe("system");
        Property(palette, "IsHighContrast").ShouldBe(true);
        Property(palette, "BorderThickness").ShouldBe(2d);
        Property(palette, "Text").ToString().ShouldBe(WpfStubs.SystemColorHex("WindowTextColor"));
        Property(palette, "Panel").ToString().ShouldBe(WpfStubs.SystemColorHex("WindowColor"));
        Property(palette, "OnAccent")
            .ToString()
            .ShouldBe(WpfStubs.SystemColorHex("HighlightTextColor"));
        Invoke(palette, "GetCategoryTint", generated.EnumValue("CategoryToken", "Edit"))
            .ToString()
            .ShouldBe(WpfStubs.SystemColorHex("HighlightColor"));
    }

    [Fact]
    [Trait("Req", "TEM-006")]
    public void Motion_durations_drop_to_zero_with_reduced_motion_except_the_flash()
    {
        using var generated = GeneratedAssembly.Load();

        TimeSpan Get(string token, bool reduce) =>
            (TimeSpan)
                generated.Invoke(
                    "Motion",
                    "Get",
                    generated.EnumValue("MotionToken", token),
                    reduce
                );

        Get("PanelOpacity", reduce: false).ShouldBe(TimeSpan.FromMilliseconds(350));
        Get("PanelOpacity", reduce: true).ShouldBe(TimeSpan.Zero);
        Get("Flash", reduce: true).ShouldBe(TimeSpan.FromMilliseconds(240));
    }

    [Fact]
    [Trait("Req", "TEM-009")]
    public void Shapes_expose_the_focus_ring_radii_and_shadows()
    {
        using var generated = GeneratedAssembly.Load();

        generated.Constant("FocusRing", "Thickness").ShouldBe(3d);
        generated.Constant("FocusRing", "Offset").ShouldBe(2d);
        generated.Constant("Radii", "Panel").ShouldBe(18d);
        var shadow = generated.Static("Shadows", "Panel");
        Invoke(shadow, "ColorIn", generated.Static("ThemePalettes", "Dark"))
            .ToString()
            .ShouldBe("#73000000");
        Invoke(shadow, "ColorIn", generated.Static("ThemePalettes", "HighContrast"))
            .ToString()
            .ShouldBe("#00000000");
    }

    private static string Source(GeneratorDriverRunResult result, string hintName) =>
        result
            .Results.Single()
            .GeneratedSources.Single(s =>
                string.Equals(s.HintName, hintName, StringComparison.Ordinal)
            )
            .SourceText.ToString();

    private static object Property(object instance, string name) =>
        instance.GetType().GetProperty(name)!.GetValue(instance)!;

    private static object Invoke(object instance, string name, params object[] arguments) =>
        instance.GetType().GetMethod(name)!.Invoke(instance, arguments)!;

    /// <summary>The generated code compiled with the WPF stand-ins and loaded in a collectible context.</summary>
    private sealed class GeneratedAssembly : IDisposable
    {
        private readonly AssemblyLoadContext _context;
        private readonly Assembly _assembly;

        private GeneratedAssembly(AssemblyLoadContext context, Assembly assembly)
        {
            _context = context;
            _assembly = assembly;
        }

        public static GeneratedAssembly Load()
        {
            var compilation = TokenGeneratorHarness.CompileWithWpfStubs(
                TokenGeneratorHarness.Run(TokenTestData.Files).GeneratedTrees
            );
            using var image = new MemoryStream();
            var emitted = compilation.Emit(image);
            emitted.Success.ShouldBeTrue(string.Join('\n', emitted.Diagnostics));
            image.Position = 0;
            var context = new AssemblyLoadContext("GeneratedTokens", isCollectible: true);
            return new GeneratedAssembly(context, context.LoadFromStream(image));
        }

        public object Static(string type, string property) =>
            GeneratedType(type).GetProperty(property)!.GetValue(null)!;

        public object Constant(string type, string field) =>
            GeneratedType(type).GetField(field)!.GetValue(null)!;

        public object Invoke(string type, string method, params object[] arguments) =>
            GeneratedType(type).GetMethod(method)!.Invoke(null, arguments)!;

        public object EnumValue(string type, string member) =>
            System.Enum.Parse(GeneratedType(type), member);

        public object EnumValue(string type, int value) =>
            System.Enum.ToObject(GeneratedType(type), value);

        public void Dispose() => _context.Unload();

        private Type GeneratedType(string name) =>
            _assembly.GetType(Generated + "." + name, throwOnError: true)!;
    }
}
