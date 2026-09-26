using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Clicalo.TestKit.Snapshots;
using Clicalo.TestKit.Windows.Rendering;

namespace Clicalo.Platform.IntegrationTests.TestKit;

/// <summary>
/// WPF render snapshots (Clicalo.TestKit.Windows): RenderTargetBitmap at a fixed DPI against golden PNGs. Runs
/// without an interactive desktop; whether hosted runners render identically is part of spike S0.
/// </summary>
public sealed class RenderSnapshotTests
{
    private static readonly Color Ink = Color.FromRgb(0x1F, 0x3A, 0x5F);
    private static readonly Color Accent = Color.FromRgb(0xE0, 0x7A, 0x1F);

    [Theory]
    [InlineData(96)]
    [InlineData(144)]
    public void A_visual_matches_its_golden_image_at_a_fixed_dpi(double dpi) =>
        // Snapshots/RenderSnapshotTests.A_visual_matches_its_golden_image_at_a_fixed_dpi.card-<dpi>.verified.png
        RenderSnapshot.Match(
            CreateCard,
            "card-" + dpi.ToString(System.Globalization.CultureInfo.InvariantCulture),
            RenderSnapshotOptions.Default with
            {
                Dpi = dpi,
            }
        );

    [Fact]
    public void The_rendered_size_follows_the_layout_size_and_the_dpi()
    {
        var png = RenderSnapshot.Render(
            CreateCard,
            RenderSnapshotOptions.Default with
            {
                Dpi = 144,
            }
        );

        var pixels = PngCodec.Decode(png);
        pixels.Width.ShouldBe(96);
        pixels.Height.ShouldBe(48);
    }

    [Fact]
    public void A_different_rendering_fails_and_writes_the_received_image_and_a_diff()
    {
        using var folder = new TemporaryDirectory();
        var location = new SnapshotLocation(folder.Path, "Sample.Render.card");
        var options = RenderSnapshotOptions.Default;
        RenderSnapshot.Match(CreateCard, location, options, SnapshotMode.Accept);

        var failure = Should.Throw<SnapshotMismatchException>(() =>
            RenderSnapshot.Match(() => CreateCard(Accent), location, options, SnapshotMode.Verify)
        );

        failure.Message.ShouldContain("does not match");
        failure.Message.ShouldContain("pixels");
        File.Exists(location.ReceivedPath("png")).ShouldBeTrue();
        File.Exists(location.ReceivedPath("diff", "png")).ShouldBeTrue();
        PngCodec.Decode(File.ReadAllBytes(location.ReceivedPath("diff", "png"))).Width.ShouldBe(64);
    }

    [Fact]
    public void Accepting_replaces_the_golden_image_and_a_match_cleans_up()
    {
        using var folder = new TemporaryDirectory();
        var location = new SnapshotLocation(folder.Path, "Sample.Render.accept");
        var options = RenderSnapshotOptions.Default with { Background = Colors.White };
        Should
            .Throw<SnapshotMismatchException>(() =>
                RenderSnapshot.Match(CreateCard, location, options, SnapshotMode.Verify)
            )
            .Message.ShouldContain("has no verified file yet");

        RenderSnapshot.Match(CreateCard, location, options, SnapshotMode.Accept);
        RenderSnapshot.Match(CreateCard, location, options, SnapshotMode.Verify);

        File.Exists(location.VerifiedPath("png")).ShouldBeTrue();
        Directory.GetFiles(folder.Path, "*.received.*").ShouldBeEmpty();
    }

    [Fact]
    public void Visuals_that_are_not_elements_need_an_explicit_size() =>
        Should.Throw<ArgumentException>(() =>
            RenderSnapshot.Render(() => new DrawingVisual(), RenderSnapshotOptions.Default)
        );

    // 64 × 32 DIPs, pixel-aligned solid shapes only: no text or antialiased edges, so the golden image is identical
    // on every machine and scale.
    private static Border CreateCard() => CreateCard(Ink);

    private static Border CreateCard(Color stripe) =>
        new()
        {
            Width = 64,
            Height = 32,
            Background = new SolidColorBrush(Color.FromRgb(0xF4, 0xF1, 0xEA)),
            BorderBrush = new SolidColorBrush(Ink),
            BorderThickness = new Thickness(2),
            SnapsToDevicePixels = true,
            Child = new Border
            {
                Margin = new Thickness(6),
                Width = 20,
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = new SolidColorBrush(stripe),
            },
        };
}
