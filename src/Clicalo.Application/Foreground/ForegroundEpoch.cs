using System.Globalization;
using System.Runtime.InteropServices;

namespace Clicalo.Application.Foreground;

/// <summary>
/// Counts the verified external foreground changes (blueprint §3.6, §7.9). A lease acquired in one epoch ends when
/// the epoch changes: the user switched apps.
/// </summary>
/// <param name="Value">Monotonic counter; zero before the first external foreground.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct ForegroundEpoch(ulong Value)
{
    /// <summary>The epoch that follows this one.</summary>
    public ForegroundEpoch Next() => new(Value + 1);

    /// <summary>The counter, for diagnostics.</summary>
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
