using System.Globalization;
using System.Text.Json;
using Clicalo.TestKit;
using Clicalo.TestKit.Windows.Input;

namespace Clicalo.Platform.IntegrationTests.Injection;

/// <summary>
/// The test injector (<see cref="KeyboardLayouts.ToScanCode"/>) and the product catalog
/// (<c>data/catalogs/keys.win32.json</c>) describe the same physical keys, and both correct <c>MapVirtualKeyEx</c>
/// (blueprint §7.7). Nothing else keeps them in step: this test fails when one of them changes a fixed key and the
/// other does not. Fixed keys have the same scan code in every layout, so it holds on any machine and needs no
/// desktop.
/// </summary>
public sealed class KeyCatalogConsistencyTests
{
    /// <summary>
    /// The only fixed keys where the two sources answer different questions, with what the test injector gives.
    /// The catalog holds the make code to send in scan-code mode; the test injector reproduces the <c>lParam</c> of a
    /// physical keystroke for the virtual key alone.
    /// </summary>
    private static readonly Dictionary<
        string,
        (ushort ScanCode, bool Extended, string Why)
    > Differ = new(StringComparer.Ordinal)
    {
        ["numlock"] = (
            0x45,
            true,
            "the make code is 0x45 without E0 (E0 45 is no key), but a physical Num Lock reports the extended flag "
                + "in lParam (Microsoft, Keyboard Input Overview, Extended-Key Flag)"
        ),
        ["num.enter"] = (
            0x1C,
            false,
            "it shares VK_RETURN with Enter, so the virtual key alone resolves to the main Enter"
        ),
        ["pause"] = (
            0x00,
            false,
            "its make code E1 1D cannot be expressed with KEYEVENTF_EXTENDEDKEY; it is always sent by virtual key"
        ),
    };

    [Fact]
    [Trait("Req", "NFR-004")]
    public void The_test_injector_and_keys_win32_json_agree_on_every_fixed_key()
    {
        var layout = KeyboardLayouts.OfCurrentThread;
        var mismatches = new List<string>();
        var differing = new List<string>();

        foreach (var (id, vk, scanCode, extended) in FixedKeys())
        {
            var expected = (scanCode, extended);
            if (Differ.TryGetValue(id, out var difference))
            {
                differing.Add(id);
                expected = (difference.ScanCode, difference.Extended);
            }

            var actual = KeyboardLayouts.ToScanCode((VirtualKeyCode)vk, layout);
            if (actual != expected)
            {
                mismatches.Add(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{id} (VK 0x{vk:X2}): test injector 0x{actual.ScanCode:X2} extended={actual.Extended}, "
                            + $"expected 0x{expected.scanCode:X2} extended={expected.extended}"
                    )
                );
            }
        }

        mismatches.ShouldBeEmpty();
        differing.Order(StringComparer.Ordinal).ShouldBe(Differ.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    [Trait("Req", "NFR-004")]
    [Trait("Req", "ATJ-004")]
    public void Num_Lock_is_sent_in_scan_code_mode_as_0x45_without_E0()
    {
        const ushort numLock = 0x90;
        var layout = KeyboardLayouts.OfCurrentThread;
        var catalog = FixedKeys()
            .Single(static key => string.Equals(key.Id, "numlock", StringComparison.Ordinal));

        (catalog.ScanCode, catalog.Extended).ShouldBe(((ushort)0x45, false));
        KeyboardLayouts
            .ToVirtualKey(0x45, extended: false, layout)
            .ShouldBe((VirtualKeyCode)numLock);
        KeyboardLayouts.ToVirtualKey(0x45, extended: true, layout).ShouldBe(VirtualKeyCode.None);
    }

    private static List<(string Id, ushort Vk, ushort ScanCode, bool Extended)> FixedKeys()
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RepoPaths.Data, "catalogs", "keys.win32.json"))
        );
        return
        [
            .. document
                .RootElement.GetProperty("keys")
                .EnumerateObject()
                .Where(static key => key.Value.TryGetProperty("vk", out _))
                .Select(static key =>
                    (
                        key.Name,
                        Hex(key.Value.GetProperty("vk")),
                        Hex(key.Value.GetProperty("scan")),
                        key.Value.GetProperty("extended").GetBoolean()
                    )
                ),
        ];
    }

    private static ushort Hex(JsonElement value) =>
        ushort.Parse(
            value.GetString()!.AsSpan(2),
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture
        );
}
