using Clicalo.Application.Coordinators;
using Clicalo.Application.Engine;
using Clicalo.Application.Session;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Keys;
using Clicalo.Domain.KeySafety;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Touch;
using Clicalo.Presentation.Panel;

namespace Clicalo.Windowing.IntegrationTests.MinimalPanel;

/// <summary>
/// The view models of the M2 panel, headless (Presentation has no test project of its own in M2): the projection of
/// one profile, the tiles that forward intentions to the controller, the panic strip that follows the engine and the
/// texts of <c>data/i18n</c> in both languages. No window, no desktop.
/// </summary>
[Trait("Req", "EJE-001")]
public sealed class PanelViewModelTests
{
    private static readonly ContactSummary Quick = new(
        TimeSpan.FromMilliseconds(80),
        1,
        PalmLike: false
    );

    private readonly PanelEngineInbox _engine = new();
    private readonly PanelViewModel _panel;

    public PanelViewModelTests()
    {
        var controller = new PanelInteractionController(_engine, () => 3, TimeProvider.System);
        _panel = new PanelViewModel(
            controller,
            PanelTestData.Localization("es"),
            new TouchSettings(TimeSpan.FromMilliseconds(300), 14, 35, TimeSpan.Zero),
            id => id == new ShortcutId("elsewhere") ? "Otra" : null
        );
        _panel.Apply(PanelProjector.Project(PanelTestData.Profile(), LangCode.Es, LangCode.Es));
    }

    [Fact]
    [Trait("Req", "PER-001")]
    [Trait("Req", "ATJ-004")]
    public void The_panel_shows_its_profile_in_order_with_names_behaviors_and_the_mode_of_the_profile()
    {
        var model = PanelProjector.Project(
            PanelTestData.Profile(InjectionMode.ScanCode),
            LangCode.En,
            LangCode.Es
        );

        model.Profile.ShouldBe(PanelTestData.Word);
        model
            .Tiles.Select(static tile => (tile.Id, tile.Name, tile.Behavior))
            .ShouldBe([
                (PanelTestData.Copy, "Copy", TileBehavior.Tap),
                (PanelTestData.HoldCtrl, "Hold Ctrl", TileBehavior.Hold),
                (PanelTestData.ShiftLock, "Shift lock", TileBehavior.Toggle),
                (PanelTestData.Site, "Site", TileBehavior.Tap),
            ]);
        model.Tiles.ShouldAllBe(tile =>
            tile.Binding.OriginProfile == PanelTestData.Word
            && tile.Binding.Injection == InjectionMode.ScanCode
        );
    }

    [Fact]
    public void A_new_projection_keeps_the_view_model_of_every_tile_that_stays()
    {
        var before = _panel.Tiles.ToList();
        var profile = PanelTestData.Profile();
        var reordered = profile with
        {
            Shortcuts = new ValueList<Clicalo.Domain.Library.Shortcut>([
                profile.Shortcuts[2],
                profile.Shortcuts[0],
            ]),
        };

        _panel.Apply(PanelProjector.Project(reordered, LangCode.Es, LangCode.Es));

        _panel
            .Tiles.Select(static tile => tile.Id)
            .ShouldBe([PanelTestData.ShiftLock, PanelTestData.Copy]);
        _panel.Tiles[0].ShouldBeSameAs(before[2]);
        _panel.Tiles[1].ShouldBeSameAs(before[0]);
    }

    [Fact]
    [Trait("Req", "EJE-003")]
    [Trait("Req", "EJE-004")]
    [Trait("Req", "EJE-005")]
    public void Every_intention_of_a_tile_reaches_the_engine_through_the_controller()
    {
        var copy = Tile(PanelTestData.Copy);
        var hold = Tile(PanelTestData.HoldCtrl);
        var shift = Tile(PanelTestData.ShiftLock);

        copy.Tapped(7, PointerKind.Pen, Quick, DateTimeOffset.UnixEpoch);
        hold.HoldStarted(8, PointerKind.Finger, DateTimeOffset.UnixEpoch);
        hold.HoldEnded(8, Quick, HoldEndReason.LeftTarget);
        shift.Invoke();

        var events = _engine.Events;
        events.Count.ShouldBe(4);
        var tap = events[0].ShouldBeOfType<EngineEvent.Activation>();
        (
            tap.Shortcut.Id,
            tap.Request.Phase,
            tap.Request.Origin,
            tap.Request.ContactId,
            tap.Epoch
        ).ShouldBe((PanelTestData.Copy, ActivationPhase.ContactEnded, ActivationOrigin.Pen, 7, 3L));
        var start = events[1].ShouldBeOfType<EngineEvent.Activation>();
        (start.Shortcut.Id, start.Request.Phase).ShouldBe(
            (PanelTestData.HoldCtrl, ActivationPhase.ContactStarted)
        );
        events[2].ShouldBe(new EngineEvent.ContactEnded(8, Quick, Cancelled: true));
        var invoke = events[3].ShouldBeOfType<EngineEvent.Activation>();
        (invoke.Shortcut.Id, invoke.Request.Origin).ShouldBe(
            (PanelTestData.ShiftLock, ActivationOrigin.UiaInvoke)
        );
    }

    [Fact]
    [Trait("Req", "SEG-002")]
    public void Without_anything_held_the_panic_strip_is_hidden_and_no_tile_is_latched()
    {
        _panel.ApplyEngine(EngineSnapshot.Empty);

        _panel.Panic.IsVisible.ShouldBeFalse();
        _panel.Tiles.ShouldAllBe(static tile =>
            !tile.IsLatched && tile.AccessibleState.Length == 0
        );
        _panel.Panic.ReleaseAllName.ShouldBe("Soltar todo");
    }

    [Fact]
    [Trait("Req", "SEG-002")]
    [Trait("Req", "EJE-007")]
    [Trait("Req", "ACC-003")]
    public void While_something_is_held_the_strip_names_it_and_each_tile_says_how_it_holds()
    {
        _panel.ApplyEngine(
            Snapshot(
                Held(PanelTestData.HoldCtrl, contact: 4),
                Held(PanelTestData.ShiftLock, contact: null),
                Held(new ShortcutId("elsewhere"), contact: null)
            )
        );

        _panel.Panic.IsVisible.ShouldBeTrue();
        _panel.Panic.HeldMessage.ShouldBe("Pulsado: Mantener Ctrl, Mayús fija, Otra");
        Tile(PanelTestData.HoldCtrl).IsLatched.ShouldBeTrue();
        Tile(PanelTestData.HoldCtrl).AccessibleState.ShouldBe("Manteniendo");
        Tile(PanelTestData.ShiftLock)
            .AccessibleState.ShouldBe("activado · toca otra vez para soltar");
        Tile(PanelTestData.Copy).IsLatched.ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "SEG-003")]
    public void Release_all_on_the_strip_asks_the_engine_to_release_everything()
    {
        _panel.Panic.ReleaseAll();

        _engine
            .Events.ShouldHaveSingleItem()
            .ShouldBe(new EngineEvent.ReleaseAll(ReleaseReason.User));
    }

    [Fact]
    [Trait("Req", "ACC-003")]
    public void Each_tile_tells_screen_readers_the_kind_of_its_action()
    {
        _panel.ApplyEngine(EngineSnapshot.Empty);

        _panel
            .Tiles.Select(static tile => tile.AccessibleHelpText)
            .ShouldBe(["Pulsar", "Mantener", "Alternar", "Pulsar"]);
    }

    [Fact]
    [Trait("Req", "IDI-001")]
    public void Changing_the_language_formats_every_text_again()
    {
        var localization = PanelTestData.Localization("es");
        var panel = new PanelViewModel(
            new PanelInteractionController(_engine, () => 0, TimeProvider.System),
            localization,
            default,
            _ => null
        );
        panel.Apply(PanelProjector.Project(PanelTestData.Profile(), LangCode.En, LangCode.Es));
        panel.ApplyEngine(Snapshot(Held(PanelTestData.ShiftLock, contact: null)));
        panel.AccessibleName.ShouldBe("Clícalo");
        panel.Panic.ReleaseAllName.ShouldBe("Soltar todo");

        localization.TrySetLanguage("en").ShouldBeTrue();
        panel.Relocalize();

        panel.Panic.ReleaseAllName.ShouldBe("Release all");
        panel.Panic.HeldMessage.ShouldBe("Held: Shift lock");
        panel.Tiles[2].AccessibleState.ShouldBe("on · tap again to release");
    }

    [Fact]
    [Trait("Req", "BUR-003")]
    public void The_panel_follows_the_presence_of_the_session()
    {
        _panel.ApplySession(new PanelSession(PanelPresence.Hidden, PanelTestData.Word, 1));
        _panel.IsVisible.ShouldBeFalse();

        _panel.ApplySession(new PanelSession(PanelPresence.Visible, PanelTestData.Word, 2));
        _panel.IsVisible.ShouldBeTrue();
    }

    private TileViewModel Tile(ShortcutId id) => _panel.Tiles.Single(tile => tile.Id == id);

    private static EngineSnapshot Snapshot(params PressedItem[] held) =>
        EngineSnapshot.Empty with
        {
            Held = new ValueList<PressedItem>([.. held]),
            Version = 1,
        };

    private static PressedItem Held(ShortcutId shortcut, int? contact) =>
        new(
            contact is { } id ? HolderId.ForContact(id) : HolderId.ForToggle(shortcut),
            contact is null ? HoldOrigin.Toggle : HoldOrigin.Contact,
            shortcut,
            contact,
            [],
            MouseButtons.None,
            0,
            null
        );
}
