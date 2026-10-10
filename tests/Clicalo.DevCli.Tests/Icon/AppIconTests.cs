using System.Xml.Linq;
using Clicalo.Design.Math;
using Clicalo.DevCli.Icon;
using Clicalo.TestKit;

namespace Clicalo.DevCli.Tests.Icon;

/// <summary>
/// <c>app-icon</c> (BUR-003): the icon of Clícalo is drawn from the logo and <c>data/tokens</c> by a tool of the
/// repository, at the usual sizes from 16 to 256, and the files in <c>assets/icons</c> are exactly what it draws: the
/// icon of the executable and of the tray, and the tray icon at 55 % for the hidden panel and the pause.
/// </summary>
public sealed class AppIconTests
{
    private static readonly Rgba8 Fill = new(0, 100, 130);
    private static readonly Rgba8 Ink = new(255, 255, 255);

    [Fact]
    [Trait("Req", "BUR-003")]
    public void The_icon_files_of_the_repository_are_what_the_tool_draws()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var code = Cli.Run(
            ["app-icon", "--check", "--repo", RepoPaths.Root],
            RepoPaths.Root,
            output,
            error
        );

        code.ShouldBe(ExitCodes.Success, output.ToString());
        output.ToString().TrimEnd().ShouldEndWith("app-icon: the 2 icon files are up to date.");
    }

    [Fact]
    [Trait("Req", "BUR-003")]
    public void The_icon_has_the_usual_sizes_from_16_to_256_and_the_tray_icon_its_own()
    {
        Sizes(AppIconCommand.IconPath).ShouldBe([16, 20, 24, 32, 40, 48, 64, 128, 256]);
        Sizes(AppIconCommand.DimIconPath).ShouldBe([16, 20, 24, 32, 40, 48, 64]);
    }

    [Fact]
    [Trait("Req", "BUR-003")]
    [Trait("Req", "BUR-004")]
    public void The_dim_tray_icon_is_the_same_icon_at_55_percent()
    {
        var normal = Images(AppIconCommand.IconPath).ToDictionary(static image => image.Size);

        foreach (var dim in Images(AppIconCommand.DimIconPath))
        {
            var full = normal[dim.Size];
            for (var i = 0; i < dim.Bgra.Length; i += 4)
            {
                // Same color, 55 % of the opacity.
                dim.Bgra.AsSpan(i, 3).SequenceEqual(full.Bgra.AsSpan(i, 3)).ShouldBeTrue();
                var expected = (int)((full.Bgra[i + 3] * AppIconCommand.DimOpacity) + 0.5);
                Math.Abs(dim.Bgra[i + 3] - expected).ShouldBeLessThanOrEqualTo(1);
            }
        }
    }

    [Fact]
    [Trait("Req", "BUR-003")]
    [Trait("Req", "TEM-008")]
    public void The_logo_is_the_accent_square_with_rounded_corners_and_the_strokes_over_it()
    {
        var image = LogoRaster.Render(48, Fill, Ink, 1);

        // The corners of the rounded square are empty.
        Pixel(image, 0, 0).A.ShouldBe((byte)0);
        Pixel(image, 47, 47).A.ShouldBe((byte)0);

        // The square is the accent, opaque.
        Pixel(image, 6, 24).ShouldBe(Fill);

        // The stem of the «í» is written in the color that goes over the accent.
        Pixel(image, 24, 30).ShouldBe(Ink);

        // The small stroke is drawn at 75 % over the accent: lighter than the accent, not white.
        var small = Pixel(image, 35, 11);
        small.G.ShouldBeGreaterThan(Fill.G);
        small.G.ShouldBeLessThan(Ink.G);
    }

    [Fact]
    [Trait("Req", "BUR-003")]
    public void The_same_colors_always_draw_the_same_pixels()
    {
        var first = LogoRaster.Render(32, Fill, Ink, AppIconCommand.DimOpacity);
        var second = LogoRaster.Render(32, Fill, Ink, AppIconCommand.DimOpacity);

        second.Bgra.ShouldBe(first.Bgra);
    }

    [Fact]
    [Trait("Req", "BUR-003")]
    public void An_icon_file_keeps_every_pixel_of_every_size()
    {
        var images = AppIconCommand.Render([16, 20, 256], Fill, Ink, 1);

        var read = IcoFile.Read(IcoFile.Write(images));

        read.Select(static image => image.Size).ShouldBe([16, 20, 256]);
        for (var i = 0; i < images.Count; i++)
        {
            read[i].Bgra.ShouldBe(images[i].Bgra);
        }
    }

    [Fact]
    [Trait("Req", "BUR-003")]
    public void A_file_that_is_not_an_icon_is_out_of_date_and_never_read_as_one()
    {
        using var folder = new TemporaryRepository();
        _ = folder.Write(AppIconCommand.IconPath, "not an icon");

        Should.Throw<InvalidDataException>(() => IcoFile.Read("not an icon"u8));
        AppIconCommand.IsCurrent(folder.Root, AppIconCommand.IconPath, []).ShouldBeFalse();
        AppIconCommand.IsCurrent(folder.Root, AppIconCommand.DimIconPath, []).ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "BUR-003")]
    public void The_executable_and_the_installer_use_the_drawn_icon()
    {
        var project = XDocument.Load(RepoPaths.Combine("src", "Clicalo.App", "Clicalo.App.csproj"));

        var icon = project.Descendants("ApplicationIcon").ShouldHaveSingleItem().Value;

        icon.Replace('\\', '/').ShouldBe("$(RepoRoot)" + AppIconCommand.IconPath);
        File.Exists(RepoPaths.Combine(AppIconCommand.IconPath.Split('/'))).ShouldBeTrue();
    }

    private static IReadOnlyList<IcoImage> Images(string path) =>
        IcoFile.Read(File.ReadAllBytes(RepoPaths.Combine(path.Split('/'))));

    private static int[] Sizes(string path) => [.. Images(path).Select(static image => image.Size)];

    private static Rgba8 Pixel(IcoImage image, int x, int y)
    {
        var at = ((y * image.Size) + x) * 4;
        return new Rgba8(
            image.Bgra[at + 2],
            image.Bgra[at + 1],
            image.Bgra[at],
            image.Bgra[at + 3]
        );
    }
}
