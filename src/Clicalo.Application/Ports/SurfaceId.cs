using System.Globalization;
using System.Runtime.InteropServices;

namespace Clicalo.Application.Ports;

/// <summary>
/// Identifies one non-activatable surface for the lifetime of the process (blueprint §3.5, §3.6). It lives in Ports,
/// not in UI.Wpf, because <see cref="ISurfaceActivationStyle"/> uses it and Application cannot see UI.Wpf; the
/// <c>NonActivatingWindow</c> of UI.Wpf exposes it as <c>Id</c>.
/// </summary>
/// <param name="Kind">Which surface.</param>
/// <param name="Instance">Distinguishes several surfaces of one kind (side windows, menus); zero for singletons.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct SurfaceId(SurfaceKind Kind, int Instance)
{
    /// <summary>Kind and instance, for diagnostics and metrics.</summary>
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Kind}#{Instance}");
}
