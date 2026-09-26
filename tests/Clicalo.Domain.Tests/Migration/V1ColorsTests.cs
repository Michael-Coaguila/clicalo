using System.Text.Json;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Migration.V1;
using Clicalo.TestKit;

namespace Clicalo.Domain.Tests.Migration;

/// <summary>v1 colours become colour categories (catalog §7.4, PQ-08, EC-MIG-05).</summary>
[Trait("Req", "MIG-004")]
public sealed class V1ColorsTests
{
    [Theory]
    [InlineData("#2980B9", "edit")] // Copiar, Pegar, Cortar.
    [InlineData("#E67E22", "hist")] // Deshacer, Rehacer.
    [InlineData("#27AE60", "file")] // Guardar, Nuevo, Abrir.
    [InlineData("#8E44AD", "sel")] // Seleccionar todo, Buscar.
    [InlineData("#C0392B", "win")] // Cerrar, Cambiar app.
    [InlineData("#E74C3C", "win")]
    [InlineData("#7F8C8D", "win")] // Grey: Bloquear, Admin. tar., Imprimir.
    [InlineData("#16A085", "voice")]
    [InlineData("#F39C12", "hist")]
    public void The_v1_palette_maps_to_the_closest_category_without_a_report_line(
        string color,
        string category
    )
    {
        var mapping = V1Colors.Map(color);

        mapping.Category.ShouldBe(new CategoryId(category));
        mapping.Origin.ShouldBe(V1ColorOrigin.Palette);
        mapping.IsReported.ShouldBeFalse();
    }

    [Theory]
    [InlineData("#55ff00", "#55FF00", "file")]
    [InlineData("#5500ff", "#5500FF", "nav")]
    [InlineData("#00ffff", "#00FFFF", "voice")]
    public void The_real_custom_colours_map_to_a_category_and_are_reported(
        string color,
        string rgb,
        string category
    )
    {
        var mapping = V1Colors.Map(color);

        mapping.Category.ShouldBe(new CategoryId(category));
        mapping.Origin.ShouldBe(V1ColorOrigin.Custom);
        mapping.Rgb.ShouldBe(rgb);
        mapping.IsReported.ShouldBeTrue();
    }

    [Theory]
    [InlineData("#abc", "#AABBCC")]
    [InlineData("#FF2980B9", "#2980B9")]
    [InlineData(" #2980b9 ", "#2980B9")]
    [InlineData("red", "#FF0000")]
    [InlineData("Grey", "#808080")]
    public void Short_alpha_and_named_colours_are_normalized(string color, string rgb) =>
        V1Colors.Map(color).Rgb.ShouldBe(rgb);

    [Theory]
    [InlineData("#12")]
    [InlineData("#GGGGGG")]
    [InlineData("rgb(1,2,3)")]
    [InlineData("chartreusish")]
    public void An_invalid_colour_is_reported_without_failing(string color)
    {
        var mapping = V1Colors.Map(color);

        mapping.Origin.ShouldBe(V1ColorOrigin.Invalid);
        mapping.IsReported.ShouldBeTrue();
        mapping.Category.ShouldBe(V1Colors.Map(null).Category);
    }

    [Fact]
    public void A_missing_colour_is_the_v1_default_and_not_reported()
    {
        var mapping = V1Colors.Map(null);

        mapping.Origin.ShouldBe(V1ColorOrigin.Default);
        mapping.Category.ShouldBe(new CategoryId("edit"));
        mapping.IsReported.ShouldBeFalse();
    }

    [Fact]
    public void The_category_hues_match_the_design_tokens()
    {
        using var tokens = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RepoPaths.Data, "tokens", "extra-tokens.json"))
        );
        var hues = tokens
            .RootElement.GetProperty("categories")
            .GetProperty("hues")
            .EnumerateObject()
            .Select(static p => (p.Name, p.Value.GetDouble()))
            .ToList();

        V1Colors.CategoryHues.Select(static c => (c.Category.Value, c.Hue)).ShouldBe(hues);
    }

    [Fact]
    public void Every_category_exists_in_the_catalog()
    {
        using var catalog = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RepoPaths.Data, "catalogs", "categories.json"))
        );
        var ids = catalog
            .RootElement.GetProperty("categories")
            .EnumerateArray()
            .Select(static c => c.GetProperty("id").GetString())
            .ToList();

        V1Colors.CategoryHues.Select(static c => c.Category.Value).ShouldBe(ids);
    }
}
