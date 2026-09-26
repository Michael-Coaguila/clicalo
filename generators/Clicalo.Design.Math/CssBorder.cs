using System.Runtime.InteropServices;

namespace Clicalo.Design.Math;

/// <summary>A border shorthand from the token data, always of the form <c>&lt;width&gt;px solid &lt;color&gt;</c>.</summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct CssBorder
{
    public CssBorder(double width, CssColor color)
    {
        Width = width;
        Color = color;
    }

    /// <summary>Thickness in device-independent pixels.</summary>
    public double Width { get; }

    public CssColor Color { get; }
}
