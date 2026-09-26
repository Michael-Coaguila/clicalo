using Clicalo.Domain.Keys;
using Clicalo.Platform.Windows.Input;

namespace Clicalo.Platform.IntegrationTests.Engine;

/// <summary>
/// The layout table the engine resolves characters with (blueprint §7.7), captured from the layout of this thread with
/// <c>VkKeyScanEx</c> and <c>MapVirtualKeyEx</c>: read-only calls, nothing is injected.
/// </summary>
[Trait("Req", "EJE-003")]
[Trait("Req", "ATJ-004")]
public sealed class KeyboardLayoutCaptureTests
{
    [Fact]
    public void The_current_layout_types_catalog_characters_with_a_key_and_a_scan_code()
    {
        var layout = KeyboardLayoutCapture.ForWindow(0);

        layout.Layout.ShouldNotBe(default);
        layout.Characters.ShouldNotBeEmpty();
        foreach (var (key, character) in layout.Characters)
        {
            key.IsCharacter.ShouldBeTrue();
            character.Vk.ShouldNotBe((ushort)0, key.Value);
            character.Scan.ShouldNotBe((ushort)0, key.Value);
        }
    }

    [Fact]
    public void A_captured_character_resolves_in_both_modes_with_the_same_scan_code()
    {
        var layout = KeyboardLayoutCapture.ForWindow(0);
        var (key, character) = layout
            .Characters.OrderBy(static c => c.Key.Value, StringComparer.Ordinal)
            .First();

        layout
            .TryResolve(new KeyStroke(key), InjectionMode.VirtualKey, out var byVk)
            .ShouldBeTrue();
        layout
            .TryResolve(new KeyStroke(key), InjectionMode.ScanCode, out var byScan)
            .ShouldBeTrue();

        byVk.ShouldBe(
            new InjectedKey(
                character.Vk,
                character.Scan,
                character.Extended,
                InjectionMode.VirtualKey
            )
        );
        byScan.ShouldBe(
            new InjectedKey(0, character.Scan, character.Extended, InjectionMode.ScanCode)
        );
    }
}
