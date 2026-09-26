using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Clicalo.TestKit.Snapshots;

namespace Clicalo.TestKit.Windows.Rendering;

/// <summary>
/// Golden-image assertions for WPF: the visual is rendered with <see cref="RenderTargetBitmap"/> at a fixed DPI and
/// compared with <c>Snapshots/&lt;TestFile&gt;.&lt;TestMethod&gt;.&lt;name&gt;.verified.png</c> next to the test source file,
/// with a per-channel tolerance and a maximum share of different pixels.
/// </summary>
/// <remarks>
/// On a mismatch it writes <c>.received.png</c> and <c>.received.diff.png</c> (differences in magenta) and throws
/// <see cref="SnapshotMismatchException"/>; with <c>CLICALO_ACCEPT_SNAPSHOTS=1</c> it replaces the verified image.
/// A result within tolerance never rewrites the verified file, so golden images do not churn.
/// The visual is created, laid out and rendered on <see cref="WpfThread"/>; it is not hosted in a window, so
/// <c>Loaded</c> does not fire.
/// </remarks>
public static class RenderSnapshot
{
    /// <summary>Extension of image snapshot files.</summary>
    public const string Extension = "png";

    private const string DiffQualifier = "diff";
    private const double StandardDpi = 96;

    /// <summary>
    /// Renders the visual built by <paramref name="createVisual"/> (called on the WPF thread) and asserts it
    /// matches snapshot <paramref name="name"/> of the calling test.
    /// </summary>
    public static void Match(
        Func<Visual> createVisual,
        string name,
        RenderSnapshotOptions? options = null,
        [CallerFilePath] string sourceFilePath = "",
        [CallerMemberName] string testName = ""
    ) =>
        Match(
            createVisual,
            SnapshotLocation.ForTest(name, sourceFilePath, testName),
            options ?? RenderSnapshotOptions.Default,
            SnapshotSettings.CurrentMode
        );

    /// <summary>Renders and asserts against an explicit <paramref name="location"/> and <paramref name="mode"/>.</summary>
    public static void Match(
        Func<Visual> createVisual,
        SnapshotLocation location,
        RenderSnapshotOptions options,
        SnapshotMode mode
    ) => MatchPng(Render(createVisual, options), location, options, mode);

    /// <summary>Renders the visual built by <paramref name="createVisual"/> to PNG bytes.</summary>
    public static byte[] Render(Func<Visual> createVisual, RenderSnapshotOptions options)
    {
        ArgumentNullException.ThrowIfNull(createVisual);
        ArgumentNullException.ThrowIfNull(options);
        return WpfThread.Invoke(() => PngCodec.Encode(RenderOnWpfThread(createVisual(), options)));
    }

    /// <summary>Asserts that already rendered PNG bytes match the verified image at <paramref name="location"/>.</summary>
    public static void MatchPng(
        byte[] receivedPng,
        SnapshotLocation location,
        RenderSnapshotOptions options,
        SnapshotMode mode
    )
    {
        ArgumentNullException.ThrowIfNull(receivedPng);
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(options);

        var verifiedPath = location.VerifiedPath(Extension);
        var received = PngCodec.Decode(receivedPng);
        var expected = File.Exists(verifiedPath)
            ? PngCodec.Decode(File.ReadAllBytes(verifiedPath))
            : null;
        var comparison = expected is null
            ? null
            : PixelComparer.Compare(expected, received, options.ChannelTolerance);

        if (comparison is not null && comparison.IsWithin(options.MaxDifferentPixelsPercent))
        {
            location.DeleteReceivedFiles();
            return;
        }

        if (mode == SnapshotMode.Accept)
        {
            location.Write(verifiedPath, receivedPng);
            location.DeleteReceivedFiles();
            return;
        }

        var receivedPath = location.ReceivedPath(Extension);
        location.Write(receivedPath, receivedPng);
        string detail;
        if (expected is null || comparison is null)
        {
            detail = "Received a " + received.Width + " × " + received.Height + " px image.";
        }
        else
        {
            var diffPath = location.ReceivedPath(DiffQualifier, Extension);
            location.Write(
                diffPath,
                PngCodec.Encode(
                    PixelComparer.CreateDiffImage(expected, received, options.ChannelTolerance)
                )
            );
            detail =
                "Verified "
                + expected.Width
                + " × "
                + expected.Height
                + " px, received "
                + received.Width
                + " × "
                + received.Height
                + " px. "
                + comparison.Describe(options.ChannelTolerance)
                + Environment.NewLine
                + "  diff:     "
                + diffPath;
        }

        throw SnapshotMismatchException.For(location, verifiedPath, receivedPath, detail);
    }

    private static RenderTargetBitmap RenderOnWpfThread(
        Visual visual,
        RenderSnapshotOptions options
    )
    {
        ArgumentNullException.ThrowIfNull(visual);
        Size size;
        if (visual is UIElement element)
        {
            element.Measure(
                new Size(
                    options.Width ?? double.PositiveInfinity,
                    options.Height ?? double.PositiveInfinity
                )
            );
            size = new Size(
                options.Width ?? element.DesiredSize.Width,
                options.Height ?? element.DesiredSize.Height
            );
            element.Arrange(new Rect(size));
            element.UpdateLayout();
        }
        else
        {
            size = new Size(
                options.Width
                    ?? throw new ArgumentException(
                        "A visual that is not a UIElement needs an explicit Width.",
                        nameof(options)
                    ),
                options.Height
                    ?? throw new ArgumentException(
                        "A visual that is not a UIElement needs an explicit Height.",
                        nameof(options)
                    )
            );
        }

        WpfThread.DrainPendingWork();
        var scale = options.Dpi / StandardDpi;
        var pixelWidth = (int)Math.Ceiling(size.Width * scale);
        var pixelHeight = (int)Math.Ceiling(size.Height * scale);
        if (pixelWidth <= 0 || pixelHeight <= 0)
        {
            throw new InvalidOperationException(
                "The visual has no area to render (" + size + " DIPs)."
            );
        }

        var bitmap = new RenderTargetBitmap(
            pixelWidth,
            pixelHeight,
            options.Dpi,
            options.Dpi,
            PixelFormats.Pbgra32
        );
        if (options.Background is { } background)
        {
            var fill = new DrawingVisual();
            using (var context = fill.RenderOpen())
            {
                context.DrawRectangle(new SolidColorBrush(background), pen: null, new Rect(size));
            }

            bitmap.Render(fill);
        }

        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }
}
