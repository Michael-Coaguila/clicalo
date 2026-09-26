using System.Diagnostics.CodeAnalysis;

namespace Clicalo.Platform.Core.Injection;

/// <summary>
/// The only <c>PInvoke.SendInput</c> of the product (blueprint §4.4, §7.7), reachable only through
/// <see cref="InjectionGate"/>. Fills <c>INPUT</c> per mode: VK with the informative scan code and
/// <c>KEYEVENTF_EXTENDEDKEY</c>; scan code mode with <c>wVk = 0</c> and <c>KEYEVENTF_SCANCODE</c>; Unicode for text.
/// </summary>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality instead of throwing NotImplementedException",
    Justification = "M2 contract; the engine package implements it (docs/testing/spikes/M2-ownership.md)."
)]
public sealed class LowLevelInjector : ILowLevelSender
{
    /// <summary>
    /// The menu mask key (<c>VK 0xE8</c>, unassigned) sent before releasing Alt or Win so neither the Start menu nor a
    /// menu bar opens.
    /// </summary>
    public const ushort MenuMaskVirtualKey = 0xE8;

    /// <inheritdoc />
    public SendResult Send(ReadOnlySpan<LowLevelInput> inputs) =>
        throw new NotImplementedException();
}
