using System.Runtime.InteropServices;

namespace Clicalo.Domain.Execution.Internal;

/// <summary>One entry of <see cref="Win32FixedKeys"/>.</summary>
/// <param name="Vk">Virtual key.</param>
/// <param name="Scan">Set-1 scan code without prefix.</param>
/// <param name="Extended">Whether the key carries the extended flag (E0 prefix).</param>
/// <param name="PrefixE1">Whether the key has the E1 prefix (Pause): it is always sent by virtual key.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct FixedKey(ushort Vk, ushort Scan, bool Extended, bool PrefixE1);
