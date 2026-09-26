using Clicalo.Domain.Catalog;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Migration.V1;
using Clicalo.Domain.Settings;
using static Clicalo.Domain.Tests.Migration.V1Docs;
using PanelSize = Clicalo.Domain.Settings.PanelSize;

namespace Clicalo.Domain.Tests.Migration;

/// <summary>
/// The decisions of the v1 conversion, before the model is built (catalog §7.4 and §7.5): every rule of the table and
/// every report line of MIG-004.
/// </summary>
public sealed class V1PlannerTests
{
    [Fact]
    [Trait("Req", "MIG-004")]
    public void Every_button_is_kept_and_separators_are_only_reported()
    {
        var document = V1File(
            Profile(
                "General",
                string.Empty,
                Hotkey("Copiar", "ctrl+c"),
                Separator(),
                Url("Correo", "https://mail.example.com"),
                App("Notas", "notepad.exe")
            )
        );

        var plan = V1Planner.Plan(document, [Primary]);

        plan.Input.ShouldBe(new V1Counts(1, 4, 1, 1, 1));
        var general = plan.Profiles.ShouldHaveSingleItem();
        general.Shortcuts.Select(static s => s.Name).ShouldBe(["Copiar", "Correo", "Notas"]);
        plan.Notes.Where(static n => n.Kind == MigrationNoteKind.Separator)
            .ShouldHaveSingleItem()
            .Profile.ShouldBe("General");
    }

    [Fact]
    [Trait("Req", "MIG-002")]
    public void Missing_values_take_the_v1_defaults()
    {
        var plan = V1Planner.Plan(General(Hotkey("Copiar", "ctrl+c")), [Primary]);

        // window_opacity 0.92 → 0.90, button_size h 62 → S, window_pos [80, 80], active_profile «General».
        plan.Settings.ShouldBe(
            new V1SettingsPlan(0.90, PanelSize.Small, 0, new MonitorPosition(Primary.Id, 140, 140))
        );
        plan.Notes.ShouldContain(static n =>
            n.Kind == MigrationNoteKind.OpacityRounded && n.Original == "0.92"
        );
        plan.Notes.ShouldNotContain(static n => n.Kind == MigrationNoteKind.PinnedProfile);
        plan.Notes.ShouldNotContain(static n => n.Kind == MigrationNoteKind.ActiveProfileMissing);
    }

    [Theory]
    [Trait("Req", "MIG-006")]
    [InlineData(0.68, 0.70, true)]
    [InlineData(0.54, 0.55, true)]
    [InlineData(0.78, 0.80, true)]
    [InlineData(0.70, 0.70, false)]
    [InlineData(0.10, 0.30, true)]
    public void The_opacity_goes_to_the_nearest_step_and_a_change_is_reported(
        double v1,
        double expected,
        bool reported
    )
    {
        var plan = V1Planner.Plan(General() with { WindowOpacity = v1 }, [Primary]);

        plan.Settings.Opacity.ShouldBe(expected);
        plan.Notes.Any(static n => n.Kind == MigrationNoteKind.OpacityRounded).ShouldBe(reported);
    }

    [Theory]
    [Trait("Req", "MIG-006")]
    [InlineData(40, PanelSize.Small)]
    [InlineData(66, PanelSize.Small)]
    [InlineData(67, PanelSize.Medium)]
    [InlineData(78, PanelSize.Medium)]
    [InlineData(79, PanelSize.Large)]
    [InlineData(110, PanelSize.Large)]
    public void The_button_height_becomes_a_size_and_is_reported(int height, PanelSize size)
    {
        var plan = V1Planner.Plan(
            General() with
            {
                ButtonSize = new V1Pair(55, height),
            },
            [Primary]
        );

        plan.Settings.Size.ShouldBe(size);
        plan.Notes.ShouldContain(n =>
            n.Kind == MigrationNoteKind.SizeChanged
            && n.Original == height.ToString(System.Globalization.CultureInfo.InvariantCulture)
        );
    }

    [Fact]
    [Trait("Req", "MIG-006")]
    public void The_window_position_goes_to_its_monitor_or_moves_to_the_primary_one()
    {
        V1Planner
            .Plan(General() with { WindowPosition = new V1Pair(743, 46) }, [Primary])
            .Settings.Position.ShouldBe(new MonitorPosition(Primary.Id, 1300, 81));

        var moved = V1Planner.Plan(
            General() with
            {
                WindowPosition = new V1Pair(1963, 290),
            },
            [Primary]
        );
        moved.Settings.Position!.MonitorId.ShouldBe(Primary.Id);
        moved.Notes.ShouldContain(static n =>
            n.Kind == MigrationNoteKind.PositionMoved && n.Original == "1963, 290"
        );
    }

    [Fact]
    [Trait("Req", "MIG-004")]
    public void Settings_without_equivalent_are_reported()
    {
        var document = General() with
        {
            WindowSize = new V1Pair(239, 250),
            EditSize = new V1Pair(391, 707),
            PinnedProfile = "General",
            UnknownKeys = ["_nota"],
        };
        var profile = document.Profiles[0] with { ButtonsPerPage = 9 };

        var plan = V1Planner.Plan(document with { Profiles = [profile] }, [Primary]);

        plan.Notes.ShouldContain(static n =>
            n.Kind == MigrationNoteKind.WindowSize && n.Original == "239, 250"
        );
        plan.Notes.ShouldContain(static n =>
            n.Kind == MigrationNoteKind.EditSize && n.Original == "391, 707"
        );
        plan.Notes.ShouldContain(static n =>
            n.Kind == MigrationNoteKind.PinnedProfile && n.Profile == "General"
        );
        plan.Notes.ShouldContain(static n =>
            n.Kind == MigrationNoteKind.ButtonsPerPage && n.Original == "9"
        );
        plan.Notes.ShouldContain(static n =>
            n.Kind == MigrationNoteKind.UnknownKey && n.Original == "_nota"
        );
    }

    [Fact]
    [Trait("Req", "MIG-006")]
    public void The_active_profile_becomes_the_last_profile()
    {
        var document = V1File(
            Profile("General", string.Empty),
            Profile("Chrome", "chrome.exe")
        ) with
        {
            ActiveProfile = "Chrome",
        };

        var plan = V1Planner.Plan(document, [Primary]);

        plan.Profiles[plan.Settings.LastProfile].Name.ShouldBe("Chrome");
        plan.Notes.ShouldContain(static n => n.Kind == MigrationNoteKind.ReturnsToGeneral);
    }

    [Fact]
    [Trait("Req", "MIG-006")]
    public void A_dangling_active_profile_falls_back_to_General_and_is_reported()
    {
        var plan = V1Planner.Plan(General() with { ActiveProfile = "Borrado" }, [Primary]);

        plan.Profiles[plan.Settings.LastProfile].IsGeneral.ShouldBeTrue();
        plan.Notes.ShouldContain(static n =>
            n.Kind == MigrationNoteKind.ActiveProfileMissing && n.Original == "Borrado"
        );
    }

    [Fact]
    [Trait("Req", "MIG-004")]
    public void Without_General_an_empty_General_is_created_first()
    {
        var plan = V1Planner.Plan(
            V1File(Profile("Chrome", "chrome.exe", Hotkey("Atrás", "alt+left"))),
            [Primary]
        );

        plan.Profiles.Count.ShouldBe(2);
        plan.Profiles[0].ShouldBe(new V1ProfilePlan(true, true, "General", null, []));
        plan.Profiles[1].Name.ShouldBe("Chrome");
        plan.Profiles[plan.Settings.LastProfile].IsGeneral.ShouldBeTrue();
        plan.Notes.ShouldContain(static n => n.Kind == MigrationNoteKind.GeneralCreated);
    }

    [Fact]
    [Trait("Req", "MIG-004")]
    public void A_General_with_a_process_hands_it_and_its_buttons_to_a_profile_of_its_own()
    {
        var document = V1File(Profile("General", "Chrome.exe", Hotkey("Atrás", "alt+left"))) with
        {
            ActiveProfile = "General",
        };

        var plan = V1Planner.Plan(document, [Primary]);

        plan.Profiles[0].IsGeneral.ShouldBeTrue();
        plan.Profiles[0].Shortcuts.ShouldBeEmpty();
        var moved = plan.Profiles[1];
        moved.IsGeneral.ShouldBeFalse();
        moved.Name.ShouldBe("General (chrome.exe)");
        moved.Process.ShouldBe("chrome.exe");
        moved.Shortcuts.ShouldHaveSingleItem().Name.ShouldBe("Atrás");
        plan.Settings.LastProfile.ShouldBe(1);
        plan.Notes.ShouldContain(static n =>
            n.Kind == MigrationNoteKind.GeneralHadProcess && n.Original == "chrome.exe"
        );
    }

    [Fact]
    [Trait("Req", "MIG-004")]
    public void A_repeated_process_stays_with_the_first_profile_and_the_others_become_manual()
    {
        var document = V1File(
            Profile("General", string.Empty),
            Profile("Chrome", "chrome.exe"),
            Profile("Navegador", "C:\\Program Files\\Google\\Chrome.EXE")
        );

        var plan = V1Planner.Plan(document, [Primary]);

        plan.Profiles[1].Process.ShouldBe("chrome.exe");
        plan.Profiles[2].Process.ShouldBeNull();
        plan.Notes.ShouldContain(static n =>
            n.Kind == MigrationNoteKind.DuplicateProcess
            && n.Profile == "Navegador"
            && n.Original == "chrome.exe"
        );
    }

    [Fact]
    [Trait("Req", "MIG-007")]
    public void Win_L_becomes_the_lock_system_action()
    {
        var plan = V1Planner.Plan(General(Hotkey("Bloquear", "win+l")), [Primary]);

        plan.Profiles[0].Shortcuts[0].Action.ShouldBe(V1Planner.LockComputer);
        plan.Notes.ShouldContain(static n =>
            n.Kind == MigrationNoteKind.LockBecameSystemAction
            && n.Button == "Bloquear"
            && n.Original == "win+l"
        );
    }

    [Fact]
    [Trait("Req", "MIG-007")]
    public void Ctrl_Shift_Esc_is_kept_with_the_special_combination_warning()
    {
        var plan = V1Planner.Plan(General(Hotkey("Admin. tar.", "ctrl+shift+esc")), [Primary]);

        plan.Profiles[0]
            .Shortcuts[0]
            .Action.ShouldBe(
                new V1ActionPlan.KeyPresses([
                    Chord(Key(KeyIds.Ctrl), Key(KeyIds.Shift), Key(KeyIds.Escape)),
                ])
            );
        plan.Notes.ShouldContain(static n =>
            n.Kind == MigrationNoteKind.SpecialCombination && n.Button == "Admin. tar."
        );
    }

    /// <summary>The 8 shortcuts of catalog §7.5 that never worked in v1, with the meaning of their name.</summary>
    public static TheoryData<string, string, KeyStroke[][]> NeverWorked() =>
        new()
        {
            {
                "Acercar",
                "ctrl++",
                [
                    [Key(KeyIds.Ctrl), Key(KeyIds.Plus)],
                ]
            },
            {
                "Inser. fila",
                "ctrl+num+",
                [
                    [Key(KeyIds.Ctrl), Key(KeyIds.NumAdd)],
                ]
            },
            {
                "Elim. fila",
                "ctrl+num-",
                [
                    [Key(KeyIds.Ctrl), Key(KeyIds.NumSubtract)],
                ]
            },
            {
                "Comentar",
                "ctrl+k ctrl+c",
                [
                    [Key(KeyIds.Ctrl), Key(KeyIds.K)],
                    [Key(KeyIds.Ctrl), Key(KeyIds.C)],
                ]
            },
            {
                "Descomentar",
                "ctrl+k ctrl+u",
                [
                    [Key(KeyIds.Ctrl), Key(KeyIds.K)],
                    [Key(KeyIds.Ctrl), Key(KeyIds.U)],
                ]
            },
        };

    [Theory]
    [Trait("Req", "MIG-007")]
    [Trait("Req", "MIG-003")]
    [MemberData(nameof(NeverWorked))]
    public void The_shortcuts_that_never_worked_get_the_meaning_of_their_name_and_are_reported(
        string label,
        string hotkey,
        KeyStroke[][] chords
    )
    {
        var plan = V1Planner.Plan(General(Hotkey(label, hotkey)), [Primary]);

        plan.Profiles[0]
            .Shortcuts[0]
            .Action.ShouldBe(
                new V1ActionPlan.KeyPresses([
                    .. chords.Select(
                        static c => new Clicalo.Domain.Primitives.ValueList<KeyStroke>([.. c])
                    ),
                ])
            );
        plan.Notes.ShouldContain(n =>
            n.Kind == MigrationNoteKind.NeverWorkedInV1 && n.Button == label && n.Original == hotkey
        );
    }

    [Fact]
    [Trait("Req", "MIG-005")]
    public void A_combination_with_an_unknown_token_is_imported_incomplete_for_review()
    {
        var plan = V1Planner.Plan(General(Hotkey("Raro", "ctrl+hyper")), [Primary]);

        plan.Profiles[0].Shortcuts[0].Action.ShouldBe(new V1ActionPlan.KeyPresses([]));
        plan.Notes.ShouldContain(static n =>
            n.Kind == MigrationNoteKind.UnresolvedToken && n.Original == "ctrl+hyper"
        );
    }

    [Fact]
    [Trait("Req", "MIG-002")]
    public void Buttons_without_action_are_imported_incomplete_and_reported()
    {
        var plan = V1Planner.Plan(
            General(Hotkey("Vacío", null), Url("Web", null), App("App", "")),
            [Primary]
        );

        plan.Profiles[0].Shortcuts.Count.ShouldBe(3);
        plan.Notes.Count(static n => n.Kind == MigrationNoteKind.MissingAction).ShouldBe(3);
    }

    [Fact]
    [Trait("Req", "MIG-002")]
    public void An_unknown_type_is_sent_as_a_combination_like_v1_did_and_reported()
    {
        var button = new V1Button(V1ButtonKind.Unknown, "Macro", "ctrl+s", null, Blue, "macro");

        var plan = V1Planner.Plan(General(button), [Primary]);

        plan.Profiles[0]
            .Shortcuts[0]
            .Action.ShouldBe(new V1ActionPlan.KeyPresses([Chord(Key(KeyIds.Ctrl), Key(KeyIds.S))]));
        plan.Notes.ShouldContain(static n =>
            n.Kind == MigrationNoteKind.UnknownButton && n.Original == "macro"
        );
    }

    [Fact]
    [Trait("Req", "MIG-004")]
    public void Custom_and_invalid_colours_are_reported_and_palette_colours_are_not()
    {
        var plan = V1Planner.Plan(
            General(
                Hotkey("A", "a", "#2980B9"),
                Hotkey("B", "b", "#55ff00"),
                Hotkey("C", "c", "papaya"),
                Hotkey("D", "d", null)
            ),
            [Primary]
        );

        plan.Profiles[0]
            .Shortcuts.Select(static s => s.Category)
            .ShouldBe([
                new CategoryId("edit"),
                new CategoryId("file"),
                new CategoryId("edit"),
                new CategoryId("edit"),
            ]);
        plan.Notes.Where(static n => n.Kind == MigrationNoteKind.HexColor)
            .Select(static n => n.Original)
            .ShouldBe(["#55ff00", "papaya"]);
    }

    [Fact]
    [Trait("Req", "LOG-008")]
    public void Web_and_app_buttons_keep_their_targets_and_risky_ones_are_reported()
    {
        var plan = V1Planner.Plan(
            General(
                Url("Correo", "https://mail.example.com"),
                Url("Local", "file:///C:/x"),
                App("Consola", "cmd /c dir")
            ),
            [Primary]
        );

        var shortcuts = plan.Profiles[0].Shortcuts;
        shortcuts[0]
            .Action.ShouldBe(
                new V1ActionPlan.Url(new UrlTarget.Valid(new Uri("https://mail.example.com")))
            );
        shortcuts[1].Action.ShouldBe(new V1ActionPlan.Url(new UrlTarget.Raw("file:///C:/x")));
        shortcuts[2].Action.ShouldBe(new V1ActionPlan.App(new AppTarget.Raw("cmd /c dir")));
        plan.Notes.ShouldContain(static n =>
            n.Kind == MigrationNoteKind.NonWebAddress && n.Button == "Local"
        );
        plan.Notes.ShouldContain(static n =>
            n.Kind == MigrationNoteKind.InterpreterCommand && n.Button == "Consola"
        );
    }

    [Fact]
    [Trait("Req", "MIG-004")]
    public void Planning_is_pure_the_same_document_gives_the_same_plan()
    {
        var document = V1File(
            Profile(
                "General",
                string.Empty,
                Hotkey("Copiar", "ctrl+c"),
                Hotkey("Bloquear", "win+l")
            ),
            Profile("VS Code", "code.exe", Hotkey("Comentar", "ctrl+k ctrl+c"), Separator())
        ) with
        {
            ActiveProfile = "VS Code",
            WindowOpacity = 0.68,
        };

        V1Planner.Plan(document, [Primary]).ShouldBe(V1Planner.Plan(document, [Primary]));
    }
}
