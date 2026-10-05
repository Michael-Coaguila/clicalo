namespace Clicalo.Domain.Execution;

/// <summary>How a character of a layout is typed (<c>VkKeyScanEx</c> and <c>MapVirtualKeyEx</c> results, captured by the platform).</summary>
/// <param name="Vk">Virtual key.</param>
/// <param name="Scan">Scan code.</param>
/// <param name="Extended">Whether the key is extended.</param>
/// <param name="NeedsShift">Whether Shift is part of the character.</param>
/// <param name="NeedsAltGr">Whether AltGr is part of the character.</param>
public readonly record struct LayoutKey(
    ushort Vk,
    ushort Scan,
    bool Extended,
    bool NeedsShift,
    bool NeedsAltGr
);
