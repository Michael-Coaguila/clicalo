using System.Collections.Immutable;
using Clicalo.Application.Ports;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Keys;
using Clicalo.Domain.KeySafety;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Platform.Core.Injection;
using Clicalo.Platform.Core.KeyLedger;
using Clicalo.Platform.IntegrationTests.Desktop;
using Clicalo.Platform.Windows.Input;
using Clicalo.TestKit.Windows;
using Clicalo.TestKit.Windows.Input;
using Clicalo.TestKit.Windows.Probe;
using KeyStroke = Clicalo.Domain.Keys.KeyStroke;

namespace Clicalo.Platform.IntegrationTests.Engine;

/// <summary>
/// Spike S7 as tests (blueprint §15.1, §7.7): what a real Win32 application receives from the product's path, the
/// Domain's resolution, the adapter, the gate with its ledger and the only <c>SendInput</c>, in virtual-key and scan
/// code mode, Unicode, the probe's own layout (es-ES on the maintainer's machine, en-US on the CI runner), sides and
/// extended keys. Every batch is balanced and goes only to the probe (<see cref="GuardedProbeSender"/>); right Ctrl
/// and AltGr only in continuous integration.
/// </summary>
[Collection(DesktopCollectionDefinition.Name)]
[Trait("Requires", "Desktop")]
[Trait("Req", "EJE-003")]
[Trait("Req", "ATJ-004")]
[Trait("Req", "NFR-004")]
public sealed class S7ProductInjectionTests(DesktopProbeFixture desktop)
{
    private static readonly EngineGeneration Generation = new(1);

    private sealed class ProductPath : IDisposable
    {
        public ProductPath(nint probe)
        {
            Ledger = KeyLedgerSection.CreateInMemory();
            Injector = new GateInputInjector(
                new InjectionGate(Ledger, new GuardedProbeSender(probe))
            );
            Layout = KeyboardLayoutCapture.ForWindow(probe);
        }

        public KeyLedgerSection Ledger { get; }

        public GateInputInjector Injector { get; }

        public KeyboardLayoutSnapshot Layout { get; }

        /// <summary>A Tap as the engine plans it: press in order, release in reverse order, in one batch.</summary>
        public void Tap(InjectionMode mode, params KeyStroke[] strokes)
        {
            var keys = strokes.Select(stroke => Resolve(stroke, mode)).ToArray();
            ImmutableArray<InjectedEvent> events =
            [
                .. keys.Select(InjectedEvent.KeyDown),
                .. keys.Reverse().Select(InjectedEvent.KeyUp),
            ];
            Injector.Send(Generation, events.AsSpan()).Status.ShouldBe(InjectionStatus.Sent);
            Ledger.Snapshot().Slots.ShouldBeEmpty();
        }

        public InjectedKey Resolve(KeyStroke stroke, InjectionMode mode)
        {
            Layout.TryResolve(stroke, mode, out var key).ShouldBeTrue(stroke.Key.Value);
            return key;
        }

        public void Dispose() => Ledger.Dispose();
    }

    [DesktopTheory]
    [InlineData(InjectionMode.VirtualKey)]
    [InlineData(InjectionMode.ScanCode)]
    public async Task Ctrl_A_is_pressed_in_order_and_released_in_reverse_order_in_both_modes(
        InjectionMode mode
    )
    {
        var cursor = await desktop.PrepareAsync();
        using var path = new ProductPath(desktop.Probe.Window);

        path.Tap(mode, new KeyStroke(KeyIds.Ctrl), new KeyStroke(KeyIds.A));
        var events = await desktop.CollectAsync(cursor, e => ProductProbeEvents.Keys(e).Count >= 4);

        var keys = ProductProbeEvents.Keys(events);
        keys.Select(static key => (key.SideVirtualKey, key.ScanCode, key.IsExtended, key.IsPress))
            .ShouldBe([
                (VirtualKeyCode.LeftControl, (byte)0x1D, false, true),
                (VirtualKeyCode.A, (byte)0x1E, false, true),
                (VirtualKeyCode.A, (byte)0x1E, false, false),
                (VirtualKeyCode.LeftControl, (byte)0x1D, false, false),
            ]);
        ProbeEvents.TypedChars(events).Select(static c => c.CodeUnit).ShouldBe(['\u0001']);
        ProductProbeEvents.ShouldHaveNoForeignKeys(events);
    }

    [DesktopTheory]
    [InlineData(InjectionMode.VirtualKey)]
    [InlineData(InjectionMode.ScanCode)]
    public async Task Shift_and_the_left_arrow_carry_the_extended_flag_of_the_catalog_in_both_modes(
        InjectionMode mode
    )
    {
        var cursor = await desktop.PrepareAsync();
        using var path = new ProductPath(desktop.Probe.Window);

        path.Tap(mode, new KeyStroke(KeyIds.Shift), new KeyStroke(new KeyId("left")));
        var events = await desktop.CollectAsync(cursor, e => ProductProbeEvents.Keys(e).Count >= 4);

        var keys = ProductProbeEvents.Keys(events);
        keys.Select(static key => (key.SideVirtualKey, key.ScanCode, key.IsExtended, key.IsPress))
            .ShouldBe([
                (VirtualKeyCode.LeftShift, (byte)0x2A, false, true),
                (VirtualKeyCode.Left, (byte)0x4B, true, true),
                (VirtualKeyCode.Left, (byte)0x4B, true, false),
                (VirtualKeyCode.LeftShift, (byte)0x2A, false, false),
            ]);
        ProductProbeEvents
            .Raw(events)
            .Where(static raw => raw.MakeCode == 0x4B)
            .Select(static raw => (raw.IsE0, raw.IsBreak))
            .ShouldBe([(true, false), (true, true)]);
        ProductProbeEvents.ShouldHaveNoForeignKeys(events);
    }

    [DesktopFact]
    [Trait("Req", "EJE-008")]
    public async Task Text_arrives_as_unicode_whatever_the_layout_and_a_line_break_as_enter()
    {
        var cursor = await desktop.PrepareAsync();
        using var path = new ProductPath(desktop.Probe.Window);

        path.Injector.TypeText(Generation, "ñÁ€😀\n").Status.ShouldBe(InjectionStatus.Sent);
        var events = await desktop.CollectAsync(cursor, e => ProbeEvents.TypedChars(e).Count >= 6);

        ProbeEvents
            .TypedChars(events)
            .Select(static c => c.CodeUnit)
            .ShouldBe(['ñ', 'Á', '€', '\uD83D', '\uDE00', '\r']);
        ProductProbeEvents
            .Keys(events)
            .Where(static key => key.VirtualKey == VirtualKeyCode.Return)
            .Select(static key => key.IsPress)
            .ShouldBe([true, false]);
        path.Ledger.Snapshot().Slots.ShouldBeEmpty();
    }

    [DesktopFact]
    [Trait("Req", "EJE-008")]
    public async Task The_characters_of_the_probe_layout_type_themselves_with_the_shift_they_need()
    {
        var cursor = await desktop.PrepareAsync();
        using var path = new ProductPath(desktop.Probe.Window);
        var typeable = path
            .Layout.Characters.Where(static c => !c.Value.NeedsAltGr)
            .OrderBy(static c => c.Key.Value, StringComparer.Ordinal)
            .Take(6)
            .ToList();
        typeable.ShouldNotBeEmpty();

        foreach (var (key, character) in typeable)
        {
            var strokes = character.NeedsShift
                ? new[] { new KeyStroke(KeyIds.Shift), new KeyStroke(key) }
                : new[] { new KeyStroke(key) };
            path.Tap(InjectionMode.VirtualKey, strokes);
        }

        var events = await desktop.CollectAsync(
            cursor,
            e => ProbeEvents.TypedChars(e).Count >= typeable.Count
        );
        ProbeEvents
            .TypedChars(events)
            .Select(static c => c.CodeUnit.ToString())
            .ShouldBe(typeable.Select(static c => c.Key.Value[KeyId.CharacterPrefix.Length..]));
        ProductProbeEvents.ShouldHaveNoForeignKeys(events);
    }

    [DesktopFact]
    [Trait("Req", "SEG-003")]
    public async Task A_safety_release_of_alt_sends_the_menu_mask_first()
    {
        var cursor = await desktop.PrepareAsync();
        using var path = new ProductPath(desktop.Probe.Window);
        var alt = path.Resolve(new KeyStroke(KeyIds.Alt), InjectionMode.VirtualKey);
        var held = KeyboardLedger.Empty.Acquire(
            new PressedItem(
                HolderId.ForContact(1),
                HoldOrigin.Contact,
                new ShortcutId("alt"),
                1,
                [alt],
                MouseButtons.None,
                0,
                null
            )
        );

        // Press and safety release in one balanced batch, as release all right after a Hold.
        ImmutableArray<InjectedEvent> events = [.. held.Events, .. held.Ledger.ReleaseAll().Events];
        path.Injector.Send(Generation, events.AsSpan()).Status.ShouldBe(InjectionStatus.Sent);
        var received = await desktop.CollectAsync(
            cursor,
            e => ProductProbeEvents.Keys(e).Count >= 4
        );

        ProductProbeEvents
            .Keys(received)
            .Select(static key => ((int)key.SideVirtualKey, key.IsPress))
            .ShouldBe([
                (0xA4, true),
                (LowLevelInjector.MenuMaskVirtualKey, true),
                (LowLevelInjector.MenuMaskVirtualKey, false),
                (0xA4, false),
            ]);
        path.Ledger.Snapshot().Slots.ShouldBeEmpty();
    }

    [DesktopFact]
    [Trait("Req", "EJE-009")]
    public async Task A_right_click_lands_at_the_centre_of_the_foreground_window()
    {
        var cursor = await desktop.PrepareAsync();
        using var path = new ProductPath(desktop.Probe.Window);

        path.Injector.Mouse(Generation, MouseOp.RightClick, target: null)
            .Status.ShouldBe(InjectionStatus.Sent);
        var events = await desktop.CollectAsync(
            cursor,
            e => ProductProbeEvents.Buttons(e).Count >= 2
        );

        ProductProbeEvents
            .Buttons(events)
            .Select(static b => (b.Button, b.IsDown))
            .ShouldBe([(MouseButton.Right, true), (MouseButton.Right, false)]);
    }

    [DesktopTheory]
    [InlineData(InjectionMode.VirtualKey)]
    [InlineData(InjectionMode.ScanCode)]
    [Trait(
        DesktopTestEnvironment.ReservedKeysTraitName,
        DesktopTestEnvironment.ReservedKeysTraitValue
    )]
    public async Task Right_ctrl_reaches_the_application_as_the_right_key_in_both_modes(
        InjectionMode mode
    )
    {
        var cursor = await desktop.PrepareAsync();
        using var path = new ProductPath(desktop.Probe.Window);

        path.Tap(mode, new KeyStroke(KeyIds.Ctrl, KeySide.Right));
        var events = await desktop.CollectAsync(cursor, e => ProductProbeEvents.Keys(e).Count >= 2);

        ProductProbeEvents
            .Keys(events)
            .Select(static key => (key.SideVirtualKey, key.IsExtended))
            .ShouldBe([(VirtualKeyCode.RightControl, true), (VirtualKeyCode.RightControl, true)]);
    }
}
