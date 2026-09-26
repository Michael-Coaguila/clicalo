using System.Runtime.InteropServices;

namespace Clicalo.Platform.Core.KeyLedger;

/// <summary>A key exactly as sent: virtual key, scan code and attributes (the Platform.Core twin of the Domain's <c>InjectedKey</c>).</summary>
/// <param name="Vk">Virtual key (0 in scan code mode).</param>
/// <param name="Scan">Scan code without the <c>E0</c> prefix.</param>
/// <param name="Attributes">Extended flag and mode.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct PhysicalKey(ushort Vk, ushort Scan, LedgerKeyAttributes Attributes);
