namespace Clicalo.DevCli.Icon;

/// <summary>One square image of an icon: 32-bit pixels with straight alpha, top row first.</summary>
/// <param name="Size">Its side, in pixels (16 to 256).</param>
/// <param name="Bgra">Blue, green, red and alpha of every pixel: <c>Size × Size × 4</c> bytes.</param>
internal sealed record IcoImage(int Size, byte[] Bgra);
