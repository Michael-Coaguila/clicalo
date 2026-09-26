using Clicalo.TestKit.Windows.Rendering;

namespace Clicalo.Platform.IntegrationTests.TestKit;

/// <summary>Per-channel tolerance and the share of different pixels (Clicalo.TestKit.Windows).</summary>
public sealed class PixelComparerTests
{
    [Fact]
    public void Identical_images_have_no_differences()
    {
        var image = PixelBuffer.Filled(10, 10, 10, 20, 30, 255);

        var comparison = PixelComparer.Compare(image, image.Clone(), channelTolerance: 0);

        comparison.DifferentPixels.ShouldBe(0);
        comparison.MaxChannelDelta.ShouldBe(0);
        comparison.FirstDifference.ShouldBeNull();
        comparison.IsWithin(0).ShouldBeTrue();
    }

    [Fact]
    public void Channel_differences_within_the_tolerance_count_as_equal()
    {
        var expected = PixelBuffer.Filled(4, 4, 100, 100, 100, 255);
        var actual = expected.Clone();
        actual.SetPixel(1, 2, 102, 98, 100, 255);

        PixelComparer.Compare(expected, actual, channelTolerance: 2).DifferentPixels.ShouldBe(0);
        var strict = PixelComparer.Compare(expected, actual, channelTolerance: 1);
        strict.DifferentPixels.ShouldBe(1);
        strict.MaxChannelDelta.ShouldBe(2);
        strict.FirstDifference.ShouldBe((1, 2));
    }

    [Fact]
    public void The_share_of_different_pixels_decides_the_result()
    {
        var expected = PixelBuffer.Filled(10, 20, 0, 0, 0, 255);
        var actual = expected.Clone();
        actual.SetPixel(0, 0, 255, 255, 255, 255);

        var comparison = PixelComparer.Compare(expected, actual, channelTolerance: 2);

        comparison.DifferentPercent.ShouldBe(0.5);
        comparison.IsWithin(0.5).ShouldBeTrue();
        comparison.IsWithin(0.4).ShouldBeFalse();
        comparison.Describe(2).ShouldContain("1 of 200 pixels (0.5 %)");
    }

    [Fact]
    public void Fully_transparent_pixels_are_equal_whatever_their_color_channels()
    {
        var expected = PixelBuffer.Filled(2, 2, 0, 0, 0, 0);
        var actual = PixelBuffer.Filled(2, 2, 255, 128, 7, 0);

        PixelComparer.Compare(expected, actual, channelTolerance: 0).DifferentPixels.ShouldBe(0);
    }

    [Fact]
    public void Different_sizes_never_match()
    {
        var comparison = PixelComparer.Compare(
            PixelBuffer.Filled(2, 2, 0, 0, 0, 255),
            PixelBuffer.Filled(3, 2, 0, 0, 0, 255),
            255
        );

        comparison.SameSize.ShouldBeFalse();
        comparison.IsWithin(100).ShouldBeFalse();
    }

    [Fact]
    public void The_diff_image_marks_different_pixels_in_magenta()
    {
        var expected = PixelBuffer.Filled(3, 1, 0, 0, 0, 255);
        var actual = expected.Clone();
        actual.SetPixel(2, 0, 0, 0, 255, 255);

        var diff = PixelComparer.CreateDiffImage(expected, actual, channelTolerance: 2);

        var pixels = diff.Pixels.Span;
        pixels.Slice(diff.Offset(2, 0), 4).ToArray().ShouldBe(new byte[] { 255, 0, 255, 255 });
        pixels.Slice(diff.Offset(0, 0), 4).ToArray().ShouldNotBe(new byte[] { 255, 0, 255, 255 });
    }
}
