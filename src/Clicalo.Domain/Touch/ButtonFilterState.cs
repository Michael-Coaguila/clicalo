using System.Runtime.InteropServices;

namespace Clicalo.Domain.Touch;

/// <summary>
/// Filter memory of one target: when it last accepted a touch. Only <see cref="TouchFilter.Evaluate"/> changes it;
/// an ignored touch never restarts the debounce window and other targets are never blocked (TAC-002).
/// </summary>
[StructLayout(LayoutKind.Auto)]
public struct ButtonFilterState
{
    /// <summary>When the target last accepted a touch; null if it never did.</summary>
    public DateTimeOffset? LastAccepted { readonly get; internal set; }
}
