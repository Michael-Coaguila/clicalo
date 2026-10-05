namespace Clicalo.Domain.Settings;

/// <summary>
/// Position of the handle along each edge, in percent (docs/02 <c>handlePosBySide</c>); moved ±10 inside
/// <see cref="SettingsSchema.DockHandlePosition"/> (GEN-010).
/// </summary>
/// <param name="Right">On the right edge.</param>
/// <param name="Left">On the left edge.</param>
/// <param name="Top">On the top edge.</param>
/// <param name="Bottom">On the bottom edge.</param>
public sealed record DockHandlePositions(int Right, int Left, int Top, int Bottom);
