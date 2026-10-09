using Clicalo.Application.Coordinators;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.PanelLayout;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.Presentation.Dock;
using Clicalo.Presentation.Panel;
using Clicalo.Windowing.IntegrationTests.MinimalPanel;
using SizeId = Clicalo.Domain.Catalog.PanelSize;

namespace Clicalo.Windowing.IntegrationTests.TabView;

/// <summary>
/// The view model of the Tab view, headless (Presentation has no test project of its own): shortcuts per page from the
/// measured space, voice numbers that continue across pages, the guide, the scroll tools and the intents. No window.
/// </summary>
public sealed class DockBarViewModelTests
{
    private readonly RecordingDockIntents _intents = new();
    private readonly DockBarViewModel _dock;

    public DockBarViewModelTests()
    {
        var controller = new PanelInteractionController(
            new PanelEngineInbox(),
            () => 3,
            TimeProvider.System
        );
        _dock = new DockBarViewModel(controller, PanelTestData.Localization("es"), _intents);
        var model = PanelProjector.Project(PanelTestData.Profile(), LangCode.Es, LangCode.Es);
        _dock.ApplyTiles(model.Tiles, [model.Tiles[0]]);
    }

    [Fact]
    [Trait("Req", "PES-007")]
    public void A_page_holds_the_shortcuts_that_fit_whole_in_the_measured_space()
    {
        _dock.Apply(State());
        _dock.PageTiles.Count.ShouldBe(4);
        _dock.HasPages.ShouldBeFalse();

        // M: tiles of 58 with a gap of 8: 130 px hold two whole tiles.
        _dock.ApplyTileSpace(130);

        _dock.PerPage.ShouldBe(2);
        _dock.PageTiles.Select(static t => t.AccessibleName).ShouldBe(["Copiar", "Mantener Ctrl"]);
        _dock.HasPages.ShouldBeTrue();
        _dock.PageLabel.ShouldBe("1/2");
        _dock.Next();
        _dock.PageLabel.ShouldBe("2/2");
        _dock.PageTiles.Select(static t => t.AccessibleName).ShouldBe(["Mayús fija", "Web"]);
    }

    [Fact]
    [Trait("Req", "PES-007")]
    public void The_preference_of_General_caps_a_page()
    {
        _dock.Apply(State() with { Dock = Dock() with { PerPage = 4 } });
        _dock.ApplyTileSpace(10_000);

        _dock.PerPage.ShouldBe(4);
    }

    [Fact]
    [Trait("Req", "ACC-009")]
    [Trait("Req", "PES-007")]
    public void Voice_numbers_continue_across_pages_and_Pinned_follows_them()
    {
        _dock.Apply(State() with { VoiceNumbers = true });
        _dock.ApplyTileSpace(130);
        _dock.Next();

        _dock.PageTiles.Select(static t => t.VoiceNumber).ShouldBe([3, 4]);
        _dock.PageTiles[0].SpokenName.ShouldBe("3 Mayús fija");
        _dock.PinnedTiles.Single().VoiceNumber.ShouldBe(5);
    }

    [Fact]
    [Trait("Req", "PES-008")]
    public void Scroll_up_and_down_are_Mantener_tools()
    {
        _dock.ScrollUp.Behavior.ShouldBe(TileBehavior.Hold);
        _dock.ScrollDown.Behavior.ShouldBe(TileBehavior.Hold);
        _dock.ScrollUp.AccessibleName.ShouldBe("Subir");
        _dock.ScrollDown.AccessibleName.ShouldBe("Bajar");
    }

    [Fact]
    [Trait("Req", "PES-015")]
    public void The_guide_shows_its_step_and_ends_with_understood()
    {
        _dock.Apply(State());
        _dock.ShowsCoach.ShouldBeTrue();
        _dock.CoachStepLabel.ShouldBe("1 / 3");
        _dock.CoachTitle.ShouldBe("Arriba: cerrar y expandir");
        _dock.CoachNextLabel.ShouldBe("Siguiente");

        _dock.Apply(State() with { CoachStep = 2 });
        _dock.CoachStepLabel.ShouldBe("3 / 3");
        _dock.CoachNextLabel.ShouldBe("Entendido");

        _dock.Apply(State() with { Flyout = DockFlyout.Pinned });
        _dock.ShowsCoach.ShouldBeFalse();
        _dock.Apply(State() with { Dock = Dock() with { CoachDone = true } });
        _dock.ShowsCoach.ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "PES-001")]
    [Trait("Req", "PES-006")]
    public void The_handle_and_the_profile_button_follow_the_view()
    {
        _dock.Apply(State());
        _dock.HandleIcon.ShouldBe("description");
        _dock.ProfileCaret.ShouldBe("expand_more");

        _dock.Apply(State() with { Frequents = true });
        _dock.HandleIcon.ShouldBe("star");
        _dock.ProfileCaret.ShouldBe("undo");

        _dock.Apply(State() with { Flyout = DockFlyout.Profiles });
        _dock.ProfileCaret.ShouldBe("close");
    }

    [Fact]
    [Trait("Req", "PES-008")]
    public void Pinned_shows_only_with_the_row_on()
    {
        _dock.Apply(State());
        _dock.ShowsPinned.ShouldBeTrue();
        _dock.Apply(State() with { ShowStripRow = false });
        _dock.ShowsPinned.ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "PES-005")]
    [Trait("Req", "PES-008")]
    public void Every_button_forwards_its_intent()
    {
        _dock.OpenBar();
        _dock.CloseBar();
        _dock.Expand();
        _dock.ShowFrequents();
        _dock.ProfileButton();
        _dock.ToggleLock();
        _dock.Search();
        _dock.Repeat();
        _dock.TogglePinned();
        _dock.ToggleSticky();
        _dock.TogglePinOpen();
        _dock.CoachNext();
        _dock.CoachSkip();
        _dock.ReleaseAll();
        _dock.MoveHandle(30);

        _intents.Calls.ShouldBe([
            "OpenBar",
            "CloseBar",
            "Expand",
            "ShowFrequents",
            "ProfileButton",
            "ToggleLock",
            "Search",
            "Repeat",
            "TogglePinned",
            "ToggleSticky",
            "TogglePinOpen",
            "CoachNext",
            "CoachSkip",
            "ReleaseAll",
            "MoveHandle 30",
        ]);
    }

    private static DockSettings Dock() =>
        new()
        {
            Side = DockSide.Right,
            HandlePositions = new DockHandlePositions(50, 50, 50, 50),
            HandleLocked = false,
            PinOpen = false,
            Gutter = false,
            PerPage = 5,
            CoachDone = false,
        };

    private static DockBarState State() =>
        new(
            Dock(),
            PanelSizes.Get(SizeId.M),
            ShowStripRow: true,
            StickyRow: false,
            VoiceNumbers: false,
            Frequents: false,
            ProfileName: "Word",
            ProfileIcon: "description",
            IsActiveApp: true,
            IsFixed: false,
            CanRepeat: false,
            DockFlyout.None,
            CoachStep: 0,
            BarOpen: true,
            AnythingHeld: false
        );

    private sealed class RecordingDockIntents : IDockIntents
    {
        public List<string> Calls { get; } = [];

        public void OpenBar() => Calls.Add(nameof(OpenBar));

        public void MoveHandle(int percent) => Calls.Add(nameof(MoveHandle) + " " + percent);

        public void CloseBar() => Calls.Add(nameof(CloseBar));

        public void Expand() => Calls.Add(nameof(Expand));

        public void ShowFrequents() => Calls.Add(nameof(ShowFrequents));

        public void ProfileButton() => Calls.Add(nameof(ProfileButton));

        public void ToggleLock() => Calls.Add(nameof(ToggleLock));

        public void Search() => Calls.Add(nameof(Search));

        public void Repeat() => Calls.Add(nameof(Repeat));

        public void TogglePinned() => Calls.Add(nameof(TogglePinned));

        public void ToggleSticky() => Calls.Add(nameof(ToggleSticky));

        public void TogglePinOpen() => Calls.Add(nameof(TogglePinOpen));

        public void CoachNext() => Calls.Add(nameof(CoachNext));

        public void CoachSkip() => Calls.Add(nameof(CoachSkip));

        public void ReleaseAll() => Calls.Add(nameof(ReleaseAll));
    }
}
