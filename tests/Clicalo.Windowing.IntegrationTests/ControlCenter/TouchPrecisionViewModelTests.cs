using Clicalo.Domain.Geometry;
using Clicalo.Domain.Settings;
using Clicalo.Domain.Touch;
using Clicalo.Presentation.ControlCenter.TouchPrecision;

namespace Clicalo.Windowing.IntegrationTests.ControlCenter;

/// <summary>
/// «Precisión táctil» headless (docs/05 §4, TAC-001, TAC-002, TAC-005, TAC-006): the presets and sliders with the
/// prototype's texts, one undo step per change, Personal keeping the values, and the test zone judging touches exactly
/// as the panel's recognizer does, with its counters and messages.
/// </summary>
public sealed class TouchPrecisionViewModelTests
{
    private static readonly PhysicalRect First = new(0, 0, 100, 200);
    private static readonly PhysicalRect Second = new(124, 0, 100, 200);

    private readonly ControlCenterTestWorld _world = new();

    private TouchPrecisionViewModel Create()
    {
        var section = new TouchPrecisionViewModel(
            new TouchPrecisionServices(
                _world.Store,
                _world.Localization,
                _world.Time,
                action => action()
            )
        );
        section.PlaceTargets([First, Second], 1);
        return section;
    }

    private TouchFilterSettings Touch => _world.Store.Current.Settings.Touch;

    [Fact]
    [Trait("Req", "TAC-001")]
    [Trait("Req", "TAC-005")]
    public void The_presets_and_sliders_show_the_values_in_use()
    {
        var screen = Create().Screen;

        screen.Title.ShouldBe("Precisión táctil");
        screen
            .Presets.Select(static p => p.Label)
            .ShouldBe(["Estándar", "Temblor leve", "Temblor fuerte", "Personal"]);
        screen.Presets.Single(static p => p.Selected).Label.ShouldBe("Temblor leve");
        screen
            .Sliders.Select(static s => s.Label)
            .ShouldBe([
                "Anti doble toque",
                "Área extra",
                "Cancelar si deslizas",
                "Contacto mínimo",
            ]);
        screen
            .Sliders.Select(static s => s.Display)
            .ShouldBe(["300 ms", "14 px", "35 px", "Desactivado"]);
        screen
            .Sliders.Select(static s => (s.Minimum, s.Maximum, s.Step))
            .ShouldBe([(0d, 1000d, 50d), (0d, 40d, 2d), (0d, 80d, 5d), (0d, 300d, 10d)]);
        screen.Sliders[0].LessName.ShouldBe("Menos: Anti doble toque");
        screen.Test.Message.ShouldBe("Toca varias veces seguidas para ver qué se filtra.");
    }

    [Fact]
    [Trait("Req", "TAC-001")]
    [Trait("Req", "TAC-005")]
    [Trait("Req", "REG-07")]
    public void A_preset_writes_its_values_and_one_undo_brings_them_back()
    {
        var section = Create();

        section.ChoosePreset("strong-tremor");

        Touch.ShouldBe(
            new TouchFilterSettings(
                "strong-tremor",
                TimeSpan.FromMilliseconds(600),
                24,
                28,
                TimeSpan.FromMilliseconds(80)
            )
        );
        section
            .Screen.Sliders[2]
            .Display.ShouldBe("28 px", "a preset value off the step is kept exact");
        _world.Store.Undo().IsSuccess.ShouldBeTrue();
        Touch.Preset.ShouldBe("mild-tremor");
        Touch.HitSlopPx.ShouldBe(14);
    }

    [Fact]
    [Trait("Req", "TAC-005")]
    public void Moving_a_slider_switches_to_personal_and_choosing_personal_keeps_the_values()
    {
        var section = Create();

        section.SetValue(TouchValue.HitSlop, 20);
        Touch.Preset.ShouldBe(SettingsSchema.PersonalTouchPreset);
        Touch.HitSlopPx.ShouldBe(20);
        section.Screen.Presets.Single(static p => p.Selected).Label.ShouldBe("Personal");

        section.SetValue(TouchValue.Debounce, 5000);
        Touch.Debounce.ShouldBe(TimeSpan.FromMilliseconds(1000), "kept inside its range");

        section.ChoosePreset("standard");
        section.ChoosePreset(SettingsSchema.PersonalTouchPreset);
        Touch.Preset.ShouldBe(SettingsSchema.PersonalTouchPreset);
        Touch.HitSlopPx.ShouldBe(8, "choosing Personal does not change the values");
        section.SetValue(TouchValue.CancelMove, 0);
        section.Screen.Sliders[2].Display.ShouldBe("Desactivado");
    }

    [Fact]
    [Trait("Req", "TAC-002")]
    [Trait("Req", "TAC-006")]
    public void The_test_zone_counts_registered_and_ignored_touches_with_their_message()
    {
        var section = Create();

        Tap(section, new PhysicalPoint(50, 100), 50);
        section.Screen.Test.Registered.ShouldBe(1);
        section.Screen.Test.Message.ShouldBe("Toque registrado");
        section.Screen.Test.Marks.ShouldBe([TestMark.Registered, TestMark.None]);

        Tap(section, new PhysicalPoint(50, 100), 50);
        section.Screen.Test.Ignored.ShouldBe(1);
        section.Screen.Test.Message.ShouldBe("Ignorado: doble toque demasiado rápido");
        section.Screen.Test.Marks[0].ShouldBe(TestMark.Ignored);
        section.Screen.Test.IsWarning.ShouldBeTrue();

        Tap(section, new PhysicalPoint(170, 100), 50);
        section.Screen.Test.Registered.ShouldBe(2, "another target is never blocked");

        _world.Time.Advance(TimeSpan.FromSeconds(1));
        Tap(section, new PhysicalPoint(110, 100), 50);
        section.Screen.Test.Registered.ShouldBe(
            3,
            "the gap within the extra area counts for the nearest"
        );

        _world.Time.Advance(TimeSpan.FromSeconds(1));
        var at = new PhysicalPoint(50, 50);
        section.TestContact(1, PointerPhase.Down, at);
        _world.Time.Advance(TimeSpan.FromMilliseconds(50));
        section.TestContact(1, PointerPhase.Move, at with { Y = 100 });
        section.TestContact(1, PointerPhase.Up, at with { Y = 100 });
        section.Screen.Test.Message.ShouldBe("Ignorado: deslizaste");

        section.ChoosePreset("strong-tremor");
        _world.Time.Advance(TimeSpan.FromSeconds(1));
        Tap(section, new PhysicalPoint(50, 100), 40);
        section.Screen.Test.Message.ShouldBe("Ignorado: contacto demasiado breve");
        section.Screen.Test.Ignored.ShouldBe(3);

        section.ResetCounters();
        section.Screen.Test.Registered.ShouldBe(0);
        section.Screen.Test.Ignored.ShouldBe(0);
        section.Screen.Test.Marks.ShouldBe([TestMark.None, TestMark.None]);
        section.Screen.Test.Message.ShouldBe("Toca varias veces seguidas para ver qué se filtra.");
    }

    [Fact]
    [Trait("Req", "TAC-002")]
    [Trait("Req", "TAC-006")]
    public void The_test_zone_judges_like_the_panel_recognizer()
    {
        var settings = TouchPrecisionViewModel.Filter(Touch);
        var zone = new TouchTestZone(settings);
        zone.SetTargets([First, Second], 1);
        var panel = new GestureRecognizer(settings, 1);
        panel.SetTargets([
            new TouchTarget(new TouchTargetId(0), First, TouchTargetKind.Tap),
            new TouchTarget(new TouchTargetId(1), Second, TouchTargetKind.Tap),
        ]);
        var start = _world.Time.GetUtcNow();
        (int X, int Y, int Ms, int Dy)[] table =
        [
            (50, 100, 0, 0),
            (50, 100, 100, 0),
            (170, 100, 150, 0),
            (110, 100, 700, 0),
            (118, 100, 1400, 0),
            (50, 60, 2200, 60),
            (50, 100, 3000, 10),
        ];
        var registered = 0;
        var ignored = 0;
        uint frame = 0;
        foreach (var (x, y, ms, dy) in table)
        {
            var down = start + TimeSpan.FromMilliseconds(ms);
            var up = down + TimeSpan.FromMilliseconds(40);
            var events = new List<GestureEvent>();
            panel.Feed(Frame(++frame, PointerPhase.Down, new(x, y), down), events);
            panel.Feed(Frame(++frame, PointerPhase.Up, new(x, y + dy), up), events);
            _ = zone.Feed(1, PointerPhase.Down, new(x, y), down);
            _ = zone.Feed(1, PointerPhase.Up, new(x, y + dy), up);
            registered += events.Count(static e => e.Kind == GestureKind.Tap);
            ignored += events.Count(static e =>
                e.Kind == GestureKind.Ignored && e.Target is not null
            );
            zone.Registered.ShouldBe(registered);
            zone.Ignored.ShouldBe(ignored);
        }

        registered.ShouldBe(5);
        ignored.ShouldBe(2);
    }

    private static PointerFrame Frame(
        uint id,
        PointerPhase phase,
        PhysicalPoint at,
        DateTimeOffset when
    ) =>
        new(
            id,
            when,
            [
                new PointerSample(
                    1,
                    PointerKind.Mouse,
                    phase,
                    at,
                    new PhysicalRect(at.X, at.Y, 1, 1),
                    when,
                    PointerInputOrigin.Unknown
                ),
            ]
        );

    private void Tap(TouchPrecisionViewModel section, PhysicalPoint at, int holdMilliseconds)
    {
        section.TestContact(1, PointerPhase.Down, at);
        _world.Time.Advance(TimeSpan.FromMilliseconds(holdMilliseconds));
        section.TestContact(1, PointerPhase.Up, at);
    }
}
