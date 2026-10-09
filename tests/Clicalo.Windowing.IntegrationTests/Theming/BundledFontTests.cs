using System.Globalization;
using System.IO;
using System.Resources;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using Clicalo.TestKit;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Resources;

namespace Clicalo.Windowing.IntegrationTests.Theming;

/// <summary>
/// TEM-005: the fonts are bundled (no network, no installed fonts), static and loadable from the assembly's
/// resources; every icon name of the data exists in the bundled Material Symbols, and an unknown name has a fallback.
/// </summary>
public sealed class BundledFontTests
{
    public static TheoryData<string, string, int> Faces =>
        new()
        {
            { AppFonts.UiFamilyName, "AtkinsonHyperlegible-Regular.ttf", 400 },
            { AppFonts.UiFamilyName, "AtkinsonHyperlegible-Bold.ttf", 700 },
            { AppFonts.MonoFamilyName, "JetBrainsMono-Medium.ttf", 500 },
            { AppFonts.SymbolsFamilyName, "MaterialSymbolsRounded-Regular.ttf", 400 },
            { AppFonts.SymbolsFilledFamilyName, "MaterialSymbolsRoundedFilled-Regular.ttf", 400 },
        };

    [Fact]
    [Trait("Req", "TEM-005")]
    public void Every_font_file_is_compiled_into_the_assembly_resources()
    {
        var assembly = typeof(AppFonts).Assembly;
        var resources = new ResourceManager(assembly.GetName().Name + ".g", assembly);

        foreach (var file in AppFonts.FileNames)
        {
            using var stream = resources.GetStream(
                "resources/fonts/" + file.ToLowerInvariant(),
                CultureInfo.InvariantCulture
            );
            var bytes = File.ReadAllBytes(RepoPaths.Combine("assets", "fonts", file));

            stream.ShouldNotBeNull(file).Length.ShouldBe(bytes.Length, file);
        }
    }

    [Theory]
    [Trait("Req", "TEM-005")]
    [MemberData(nameof(Faces))]
    public void Each_family_resolves_to_its_bundled_face(string family, string file, int weight) =>
        WpfThread.Invoke(() =>
        {
            var fontFamily = family switch
            {
                AppFonts.UiFamilyName => AppFonts.Ui,
                AppFonts.MonoFamilyName => AppFonts.Mono,
                AppFonts.SymbolsFamilyName => AppFonts.Symbols,
                _ => AppFonts.SymbolsFilled,
            };
            var typeface = new Typeface(
                fontFamily,
                FontStyles.Normal,
                FontWeight.FromOpenTypeWeight(weight),
                FontStretches.Normal
            );

            typeface.TryGetGlyphTypeface(out var glyphs).ShouldBeTrue(family);
            glyphs.FontUri.ToString().ShouldEndWith("Resources/Fonts/" + file, Case.Insensitive);
            glyphs.Weight.ToOpenTypeWeight().ShouldBe(weight);
            glyphs.FamilyNames.Values.ShouldContain(name => name == family);
        });

    [Fact]
    [Trait("Req", "TEM-005")]
    public void Every_icon_name_of_the_data_is_bundled()
    {
        var names = IconNamesOfTheData();

        names.Count.ShouldBeGreaterThan(100);
        names.Where(name => !MaterialSymbols.Contains(name)).ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "TEM-005")]
    public void Every_bundled_icon_has_a_glyph_in_both_fonts() =>
        WpfThread.Invoke(() =>
        {
            MaterialSymbols.Names.Count.ShouldBeGreaterThan(100);
            foreach (var name in MaterialSymbols.Names)
            {
                SymbolIcon.GlyphIndexOf(name, filled: false).ShouldNotBeNull(name);
                SymbolIcon.GlyphIndexOf(name, filled: false).ShouldNotBe((ushort)0, name);
                SymbolIcon.GlyphIndexOf(name, filled: true).ShouldNotBe((ushort)0, name);
            }
        });

    [Fact]
    [Trait("Req", "TEM-005")]
    public void An_unknown_name_draws_the_fallback_icon()
    {
        MaterialSymbols.Contains(MaterialSymbols.FallbackName).ShouldBeTrue();
        MaterialSymbols.Contains("no_such_icon").ShouldBeFalse();
        MaterialSymbols.Contains(null).ShouldBeFalse();
        MaterialSymbols
            .TryGetCodePoint(MaterialSymbols.FallbackName, out var fallback)
            .ShouldBeTrue();
        MaterialSymbols.CodePointOrFallback("no_such_icon").ShouldBe(fallback);
        MaterialSymbols.CodePointOrFallback(null).ShouldBe(fallback);
    }

    [Fact]
    [Trait("Req", "TEM-005")]
    public void The_filled_variant_fills_the_icons_that_have_an_outline() =>
        WpfThread.Invoke(() =>
        {
            var outlined = Area("star", filled: false);
            var filled = Area("star", filled: true);

            filled.ShouldBeGreaterThan(outlined * 1.2);
        });

    [Fact]
    [Trait("Req", "TEM-005")]
    public void The_codepoint_table_is_the_one_shipped_with_the_fonts()
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(RepoPaths.Combine("assets", "fonts", "codepoints.json"))
        );
        var icons = document.RootElement.GetProperty("icons");

        icons.EnumerateObject().Count().ShouldBe(MaterialSymbols.Names.Count);
        MaterialSymbols.TryGetCodePoint("bolt", out var bolt).ShouldBeTrue();
        bolt.ShouldBe(Convert.ToInt32(icons.GetProperty("bolt").GetString(), 16));
    }

    private static double Area(string symbol, bool filled)
    {
        var family = filled ? AppFonts.SymbolsFilled : AppFonts.Symbols;
        new Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal)
            .TryGetGlyphTypeface(out var glyphs)
            .ShouldBeTrue();
        var index = SymbolIcon.GlyphIndexOf(symbol, filled).ShouldNotBeNull();
        return glyphs.GetGlyphOutline(index, 96, 96).GetArea();
    }

    /// <summary>Every icon name the catalogs and the starter content can show.</summary>
    private static HashSet<string> IconNamesOfTheData()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var files = Directory
            .EnumerateFiles(Path.Combine(RepoPaths.Data, "catalogs"), "*.json")
            .Concat(
                Directory.EnumerateFiles(
                    Path.Combine(RepoPaths.Data, "content"),
                    "*.json",
                    SearchOption.AllDirectories
                )
            );
        foreach (var file in files)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            Collect(document.RootElement, names);
        }

        using var icons = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RepoPaths.Data, "catalogs", "icons.json"))
        );
        var root = icons.RootElement;
        foreach (var icon in root.GetProperty("icons").EnumerateArray())
        {
            names.Add(icon.GetProperty("id").GetString()!);
        }

        foreach (var list in new[] { "featured", "profileFeatured" })
        {
            foreach (var name in root.GetProperty(list).EnumerateArray())
            {
                names.Add(name.GetString()!);
            }
        }

        foreach (var name in root.GetProperty("defaults").EnumerateObject())
        {
            names.Add(name.Value.GetString()!);
        }

        return names;
    }

    private static void Collect(JsonElement element, HashSet<string> names)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (
                        property.Name.Equals("icon", StringComparison.Ordinal)
                        && property.Value.ValueKind == JsonValueKind.String
                    )
                    {
                        names.Add(property.Value.GetString()!);
                    }

                    Collect(property.Value, names);
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    Collect(item, names);
                }

                break;
            default:
                break;
        }
    }
}
