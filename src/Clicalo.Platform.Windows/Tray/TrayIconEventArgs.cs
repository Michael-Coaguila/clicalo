using Clicalo.Domain.Geometry;

namespace Clicalo.Platform.Windows.Tray;

/// <summary>Data of the <see cref="TrayIcon"/> events.</summary>
/// <param name="position">Cursor position when the icon was used, in physical screen pixels.</param>
public sealed class TrayIconEventArgs(PhysicalPoint position) : EventArgs
{
    /// <summary>Cursor position when the icon was used, in physical screen pixels.</summary>
    public PhysicalPoint Position { get; } = position;
}
