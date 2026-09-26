using Clicalo.Platform.IntegrationTests.Desktop;
using Clicalo.TestKit.Windows.Input;
using Clicalo.TestKit.Windows.Probe;

namespace Clicalo.Platform.IntegrationTests.Injection;

/// <summary>
/// S7-lite (blueprint §15.1 S7, §7.7): what a real Win32 application receives when keys are sent with
/// <c>SendInput</c> in virtual-key, scan-code and Unicode modes. The test injector only ever sends balanced batches
/// to the probe, after checking that the probe owns the foreground.
/// </summary>
/// <remarks>
/// These tests pin the Windows contract that the product injector (Platform.Core, milestone M2) must build on;
/// when it exists they are re-pointed at it. The full S7 adds es-ES, en-US and es-419 layouts and elevation.
/// </remarks>
[Collection(DesktopCollectionDefinition.Name)]
[Trait("Requires", "Desktop")]
public sealed class KeyboardInjectionTests(DesktopProbeFixture desktop)
{
    private const ushort LeftArrowScanCode = 0x4B;
    private const ushort ControlScanCode = 0x1D;
    private const ushort AltScanCode = 0x38;

    [DesktopFact]
    [Trait("Req", "EJE-003")]
    [Trait("Req", "NFR-004")]
    public async Task Ctrl_A_is_pressed_in_order_and_released_in_reverse_order()
    {
        var cursor = await desktop.PrepareAsync();
        var batch = KeyStrokes.Chord(VirtualKeyCode.LeftControl, VirtualKeyCode.A);
        var sent = desktop.Injector.Resolve(batch);

        desktop.Injector.Send(batch);
        var events = await CollectInjectedKeysAsync(cursor, count: 4);

        var keys = ProbeEvents.InjectedKeys(events);
        keys.Select(key => (key.VirtualKey, key.IsPress))
            .ShouldBe([
                (VirtualKeyCode.Control, true),
                (VirtualKeyCode.A, true),
                (VirtualKeyCode.A, false),
                (VirtualKeyCode.Control, false),
            ]);
        keys.Select(key => key.SideVirtualKey)
            .ShouldBe([
                VirtualKeyCode.LeftControl,
                VirtualKeyCode.A,
                VirtualKeyCode.A,
                VirtualKeyCode.LeftControl,
            ]);
        keys.Select(key => key.Modifiers)
            .ShouldBe([
                SideModifiers.LeftControl,
                SideModifiers.LeftControl,
                SideModifiers.LeftControl,
                SideModifiers.None,
            ]);

        // The informative scan codes reach the application, and Raw Input sees the same physical order.
        keys.Select(key => (ushort)key.ScanCode).ShouldBe(sent.Select(stroke => stroke.ScanCode));
        ProbeEvents
            .InjectedRaw(events)
            .Select(raw => (raw.MakeCode, raw.IsBreak))
            .ShouldBe(sent.Select(stroke => (stroke.ScanCode, stroke.IsKeyUp)));

        // Ctrl+A is translated into the control character U+0001, exactly once.
        ProbeEvents.TypedChars(events).Select(message => message.CodeUnit).ShouldBe(['\u0001']);
        ProbeEvents.ShouldHaveNoForeignKeys(events);
    }

    [DesktopFact]
    [Trait("Req", "NFR-004")]
    public async Task Left_and_right_Ctrl_reach_the_application_as_different_keys()
    {
        var cursor = await desktop.PrepareAsync();

        desktop.Injector.Send(KeyStrokes.Chord(VirtualKeyCode.LeftControl));
        desktop.Injector.Send(KeyStrokes.Chord(VirtualKeyCode.RightControl));
        var events = await CollectInjectedKeysAsync(cursor, count: 4);

        var keys = ProbeEvents.InjectedKeys(events);
        keys.ShouldAllBe(key =>
            key.VirtualKey == VirtualKeyCode.Control && key.ScanCode == ControlScanCode
        );
        keys.Select(key => (key.SideVirtualKey, key.IsExtended, key.IsPress))
            .ShouldBe([
                (VirtualKeyCode.LeftControl, false, true),
                (VirtualKeyCode.LeftControl, false, false),
                (VirtualKeyCode.RightControl, true, true),
                (VirtualKeyCode.RightControl, true, false),
            ]);
        keys[0].Modifiers.ShouldBe(SideModifiers.LeftControl);
        keys[2].Modifiers.ShouldBe(SideModifiers.RightControl);
        keys[^1].Modifiers.ShouldBe(SideModifiers.None);
        ProbeEvents
            .InjectedRaw(events)
            .Select(raw => (raw.MakeCode, raw.IsE0, raw.IsBreak))
            .ShouldBe([
                (ControlScanCode, false, false),
                (ControlScanCode, false, true),
                (ControlScanCode, true, false),
                (ControlScanCode, true, true),
            ]);
        ProbeEvents.ShouldHaveNoForeignKeys(events);
    }

    [DesktopFact]
    [Trait("Req", "NFR-004")]
    public async Task Virtual_key_mode_sends_the_arrow_keys_as_extended_keys()
    {
        var cursor = await desktop.PrepareAsync();

        // Without the extended flag the arrow would be the keypad key: Raw Input readers see keypad 4 and, with
        // Num Lock on, Windows wraps Shift+keypad in synthetic Shift releases that break text selection.
        desktop.Injector.Send(KeyStrokes.Chord(VirtualKeyCode.LeftShift, VirtualKeyCode.Left));
        var events = await CollectInjectedKeysAsync(cursor, count: 4);

        var keys = ProbeEvents.InjectedKeys(events);
        keys.Select(key => (key.SideVirtualKey, key.ScanCode, key.IsExtended, key.IsPress))
            .ShouldBe([
                (VirtualKeyCode.LeftShift, (byte)0x2A, false, true),
                (VirtualKeyCode.Left, (byte)LeftArrowScanCode, true, true),
                (VirtualKeyCode.Left, (byte)LeftArrowScanCode, true, false),
                (VirtualKeyCode.LeftShift, (byte)0x2A, false, false),
            ]);
        keys[1].Modifiers.ShouldBe(SideModifiers.LeftShift);
        ProbeEvents
            .InjectedRaw(events)
            .Where(raw => raw.MakeCode == LeftArrowScanCode)
            .Select(raw => (raw.IsE0, raw.IsBreak))
            .ShouldBe([(true, false), (true, true)]);
        ProbeEvents.ShouldHaveNoForeignKeys(events);
    }

    [DesktopFact]
    [Trait("Req", "NFR-004")]
    public async Task AltGr_is_sent_as_the_right_Alt_key()
    {
        var cursor = await desktop.PrepareAsync();

        desktop.Injector.Send(KeyStrokes.Chord(VirtualKeyCode.RightMenu));
        var events = await desktop.CollectAsync(
            cursor,
            received =>
                received
                    .OfType<KeyMessageEvent>()
                    .Count(key => key.VirtualKey == VirtualKeyCode.Menu) >= 2
        );

        var alt = events
            .OfType<KeyMessageEvent>()
            .Where(key => key.VirtualKey == VirtualKeyCode.Menu)
            .ToList();
        alt.Select(key => (key.SideVirtualKey, key.ScanCode, key.IsExtended, key.IsPress))
            .ShouldBe([
                (VirtualKeyCode.RightMenu, (byte)AltScanCode, true, true),
                (VirtualKeyCode.RightMenu, (byte)AltScanCode, true, false),
            ]);
        alt.ShouldAllBe(key => key.ExtraInfo == TestKeyboardInjector.ExtraInfoMarker);
        alt[0].Modifiers.HasFlag(SideModifiers.RightAlt).ShouldBeTrue();
        alt[0].Modifiers.HasFlag(SideModifiers.LeftAlt).ShouldBeFalse();

        // Layouts with AltGr (es-ES, es-419...) add a synthetic left Ctrl around right Alt; nothing else may appear,
        // and everything must be released at the end.
        var others = events
            .OfType<KeyMessageEvent>()
            .Where(key => key.VirtualKey != VirtualKeyCode.Menu)
            .ToList();
        others.ShouldAllBe(key => key.SideVirtualKey == VirtualKeyCode.LeftControl);
        others.Count(key => key.IsPress).ShouldBe(others.Count(key => key.IsRelease));
        events.OfType<KeyMessageEvent>().Last().Modifiers.ShouldBe(SideModifiers.None);
        TestContext.Current.TestOutputHelper?.WriteLine(
            others.Count == 0
                ? "The probe's layout has no AltGr: right Alt arrived alone."
                : "The probe's layout has AltGr: Windows added a synthetic left Ctrl."
        );
        ProbeEvents.ShouldHaveNoForeignKeys(
            events,
            allowed: key => key.SideVirtualKey == VirtualKeyCode.LeftControl
        );
    }

    [DesktopFact]
    [Trait("Req", "EJE-008")]
    [Trait("Req", "NFR-004")]
    public async Task Unicode_text_arrives_character_by_character_whatever_the_layout()
    {
        const string text = "ñ á € 😀";
        var cursor = await desktop.PrepareAsync();

        desktop.Injector.Send(KeyStrokes.UnicodeText(text));
        var events = await desktop.CollectAsync(
            cursor,
            received => ProbeEvents.TypedChars(received).Count >= text.Length
        );

        var typed = ProbeEvents.TypedChars(events);
        typed.Select(message => message.CodeUnit).ShouldBe(text.ToCharArray());
        string.Concat(typed.Select(message => message.Text)).ShouldBe(text);

        // The emoji arrives as two WM_CHAR: the first (high surrogate) completes nothing, the second carries both.
        typed[^2].Text.ShouldBeNull();
        typed[^1].Text.ShouldBe("😀");

        // Every unit is a VK_PACKET press and release, independent of the keyboard layout.
        var keys = ProbeEvents.InjectedKeys(events);
        keys.Count.ShouldBe(text.Length * 2);
        keys.ShouldAllBe(key => key.VirtualKey == VirtualKeyCode.Packet);
        keys[^1].Modifiers.ShouldBe(SideModifiers.None);
        ProbeEvents.ShouldHaveNoForeignKeys(events);
    }

    [DesktopTheory]
    [Trait("Req", "ATJ-004")]
    [Trait("Req", "NFR-004")]
    [InlineData(LeftArrowScanCode, true, VirtualKeyCode.Left)]
    [InlineData((ushort)0x1E, false, VirtualKeyCode.None)]
    public async Task Scan_code_mode_delivers_the_scan_code_the_extended_flag_and_the_layout_key(
        ushort scanCode,
        bool extended,
        VirtualKeyCode layoutIndependentKey
    )
    {
        var cursor = await desktop.PrepareAsync();
        var expectedKey = KeyboardLayouts.ToVirtualKey(
            scanCode,
            extended,
            desktop.Probe.Ready.KeyboardLayout
        );
        if (layoutIndependentKey != VirtualKeyCode.None)
        {
            expectedKey.ShouldBe(layoutIndependentKey);
        }

        desktop.Injector.Send(KeyStrokes.ScanCodeTap(scanCode, extended));
        var events = await CollectInjectedKeysAsync(cursor, count: 2);

        var keys = ProbeEvents.InjectedKeys(events);
        keys.Select(key => key.IsPress).ShouldBe([true, false]);
        keys.ShouldAllBe(key =>
            key.ScanCode == scanCode && key.IsExtended == extended && key.VirtualKey == expectedKey
        );
        ProbeEvents
            .InjectedRaw(events)
            .Select(raw => (raw.MakeCode, raw.IsE0, raw.IsBreak))
            .ShouldBe([(scanCode, extended, false), (scanCode, extended, true)]);
        ProbeEvents.ShouldHaveNoForeignKeys(events);
    }

    private Task<IReadOnlyList<ProbeEvent>> CollectInjectedKeysAsync(int cursor, int count) =>
        desktop.CollectAsync(cursor, received => ProbeEvents.InjectedKeys(received).Count >= count);
}
