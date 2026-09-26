using Clicalo.Domain.Execution;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Tests.Execution.Support;

namespace Clicalo.Domain.Tests.Execution;

/// <summary>
/// Resolution of a stroke to the physical key (blueprint §7.7, D24): fixed keys from keys.win32.json in both modes,
/// sides as the left or right key, Pause always by virtual key, characters from the foreground layout.
/// </summary>
[Trait("Req", "EJE-003")]
[Trait("Req", "ATJ-004")]
public sealed class KeyboardLayoutSnapshotTests
{
    private static InjectedKey Resolve(
        string key,
        InjectionMode mode,
        KeySide side = KeySide.Any,
        KeyboardLayoutSnapshot? layout = null
    )
    {
        (layout ?? Layouts.Spanish)
            .TryResolve(new KeyStroke(new KeyId(key), side), mode, out var resolved)
            .ShouldBeTrue(key);
        return resolved;
    }

    [Fact]
    public void A_generic_modifier_is_its_left_key() =>
        Resolve("ctrl", InjectionMode.VirtualKey)
            .ShouldBe(new InjectedKey(0xA2, 0x1D, false, InjectionMode.VirtualKey));

    [Fact]
    public void A_side_selects_the_right_key_in_both_modes()
    {
        Resolve("ctrl", InjectionMode.VirtualKey, KeySide.Right)
            .ShouldBe(new InjectedKey(0xA3, 0x1D, true, InjectionMode.VirtualKey));
        Resolve("ctrl", InjectionMode.ScanCode, KeySide.Right)
            .ShouldBe(new InjectedKey(0, 0x1D, true, InjectionMode.ScanCode));
        Resolve("alt", InjectionMode.VirtualKey, KeySide.Right)
            .ShouldBe(new InjectedKey(0xA5, 0x38, true, InjectionMode.VirtualKey));
        Resolve("win", InjectionMode.VirtualKey, KeySide.Left)
            .ShouldBe(new InjectedKey(0x5B, 0x5B, true, InjectionMode.VirtualKey));
    }

    [Fact]
    public void A_sided_key_named_directly_resolves_to_itself() =>
        Resolve("altgr", InjectionMode.VirtualKey, KeySide.Right)
            .ShouldBe(new InjectedKey(0xA5, 0x38, true, InjectionMode.VirtualKey));

    [Fact]
    public void Scan_code_mode_sends_no_virtual_key_and_keeps_the_extended_flag_of_the_table()
    {
        Resolve("left", InjectionMode.ScanCode)
            .ShouldBe(new InjectedKey(0, 0x4B, true, InjectionMode.ScanCode));
        Resolve("num.enter", InjectionMode.ScanCode)
            .ShouldBe(new InjectedKey(0, 0x1C, true, InjectionMode.ScanCode));
        Resolve("numlock", InjectionMode.ScanCode)
            .ShouldBe(new InjectedKey(0, 0x45, false, InjectionMode.ScanCode));
    }

    [Fact]
    public void Virtual_key_mode_carries_the_informative_scan_code_of_the_table() =>
        Resolve("left", InjectionMode.VirtualKey)
            .ShouldBe(new InjectedKey(0x25, 0x4B, true, InjectionMode.VirtualKey));

    [Theory]
    [InlineData(InjectionMode.VirtualKey)]
    [InlineData(InjectionMode.ScanCode)]
    public void Pause_is_always_sent_by_virtual_key(InjectionMode mode) =>
        Resolve("pause", mode).ShouldBe(new InjectedKey(0x13, 0, false, InjectionMode.VirtualKey));

    [Fact]
    public void A_character_comes_from_the_foreground_layout()
    {
        Resolve("char:ñ", InjectionMode.VirtualKey)
            .ShouldBe(new InjectedKey(0xC0, 0x27, false, InjectionMode.VirtualKey));
        Resolve("char:ñ", InjectionMode.ScanCode)
            .ShouldBe(new InjectedKey(0, 0x27, false, InjectionMode.ScanCode));
    }

    [Fact]
    [Trait("Req", "EJE-008")]
    public void A_character_missing_from_the_layout_does_not_resolve()
    {
        Layouts
            .English.TryResolve(new KeyStroke(new KeyId("char:ñ")), InjectionMode.VirtualKey, out _)
            .ShouldBeFalse();
        KeyboardLayoutSnapshot
            .Empty.TryResolve(new KeyStroke(new KeyId("char:+")), InjectionMode.VirtualKey, out _)
            .ShouldBeFalse();
    }

    [Fact]
    public void A_key_outside_the_catalog_does_not_resolve() =>
        KeyboardLayoutSnapshot
            .Empty.TryResolve(new KeyStroke(new KeyId("nokey")), InjectionMode.VirtualKey, out _)
            .ShouldBeFalse();

    [Fact]
    public void Fixed_keys_resolve_without_any_layout() =>
        KeyboardLayoutSnapshot
            .Empty.TryResolve(new KeyStroke(KeyIds.F4), InjectionMode.VirtualKey, out var key)
            .ShouldBeTrue();
}
