using System.Globalization;

namespace Clicalo.Tools.SpikeLab.Reporting;

/// <summary>One monitor of the machine, in physical pixels: what S1 row 30 (ACC-008) needs to be judged later.</summary>
/// <param name="Left">Left edge of the monitor on the virtual desktop.</param>
/// <param name="Top">Top edge of the monitor on the virtual desktop.</param>
/// <param name="Width">Resolution, horizontal.</param>
/// <param name="Height">Resolution, vertical.</param>
/// <param name="WorkWidth">Width of its work area (without the taskbar).</param>
/// <param name="WorkHeight">Height of its work area (without the taskbar).</param>
/// <param name="Dpi">Effective DPI (96 is 100 %).</param>
/// <param name="IsPrimary">True for the primary monitor.</param>
internal sealed record MonitorDescription(
    int Left,
    int Top,
    int Width,
    int Height,
    int WorkWidth,
    int WorkHeight,
    int Dpi,
    bool IsPrimary
)
{
    /// <summary>The scale Windows shows in its settings (175 for 168 DPI).</summary>
    public int ScalePercent => (int)Math.Round(Dpi * 100.0 / 96.0);

    /// <summary>«2400 × 1600 al 175 % (principal; área de trabajo 2400 × 1516)».</summary>
    public string Describe() =>
        string.Create(
            CultureInfo.GetCultureInfo("es-ES"),
            $"{Width} × {Height} al {ScalePercent} % ({(IsPrimary ? "principal; " : string.Empty)}área de trabajo {WorkWidth} × {WorkHeight})"
        );
}
