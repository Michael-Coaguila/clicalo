namespace Clicalo.Domain.Keys;

/// <summary>
/// A physical key as it was pressed (blueprint §6.1, §7.3): virtual key, scan code, extended flag and mode, resolved
/// at press time against the foreground layout. It is what the ledgers record, so a key is released exactly as it
/// was pressed even if the layout changes in between (INV-12). The same key pressed in both modes is two holdings.
/// </summary>
/// <param name="Vk">Virtual key (0 in <see cref="InjectionMode.ScanCode"/>).</param>
/// <param name="Scan">Scan code, without the <c>E0</c> prefix.</param>
/// <param name="Extended">Whether the key carries the extended flag.</param>
/// <param name="Mode">How it was sent.</param>
public readonly record struct InjectedKey(
    ushort Vk,
    ushort Scan,
    bool Extended,
    InjectionMode Mode
);
