using System.Windows.Media.Imaging;

namespace Clicalo.UI.Wpf.Windowing;

/// <summary>
/// A precomputed shadow (<see cref="ShadowRaster"/>): a frozen premultiplied bitmap in physical pixels, and how far it
/// reaches beyond each side of the surface.
/// </summary>
/// <param name="Bitmap">The shadow; transparent where the surface is.</param>
/// <param name="Left">Physical pixels left of the surface.</param>
/// <param name="Top">Physical pixels above the surface.</param>
/// <param name="Right">Physical pixels right of the surface.</param>
/// <param name="Bottom">Physical pixels below the surface.</param>
public sealed record ShadowImage(BitmapSource Bitmap, int Left, int Top, int Right, int Bottom);
