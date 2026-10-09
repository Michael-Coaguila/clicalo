using System.Text.Json;
using Clicalo.Application.UseCases.Editor;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Privacy;
using Clicalo.Domain.Timing;
using Clicalo.TestKit;

namespace Clicalo.Application.Tests.UseCases.Editor;

/// <summary>
/// The pure rules of the editor: kinds (EDI-006), Web and App targets (EDI-014), combination warnings (EDI-007),
/// positions (EDI-016) and macro steps (EDI-013).
/// </summary>
public sealed class EditorRulesTests
{
    private static readonly KeyChord CtrlC = KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.C);

    [Fact]
    [Trait("Req", "EDI-006")]
    public void The_grid_has_the_eight_kinds_in_order() =>
        ActionKinds.Grid.ShouldBe([
            ActionKind.Tap,
            ActionKind.Hold,
            ActionKind.Toggle,
            ActionKind.Text,
            ActionKind.Mouse,
            ActionKind.Macro,
            ActionKind.Url,
            ActionKind.App,
        ]);

    [Fact]
    [Trait("Req", "EDI-006")]
    public void Press_hold_and_toggle_keep_the_keys_and_the_rest_start_empty()
    {
        var tap = new TapAction(CtrlC, []);

        ActionKinds.Switch(tap, ActionKind.Hold, null).ShouldBe(new HoldAction(CtrlC));
        ActionKinds
            .Switch(new ToggleAction(CtrlC), ActionKind.Tap, null)
            .ShouldBe(new TapAction(CtrlC, []));
        ActionKinds
            .Switch(tap, ActionKind.Mouse, null)
            .ShouldBe(new MouseAction(MouseOp.RightClick, ScrollSpeed.Normal));
        ActionKinds
            .Switch(tap, ActionKind.Text, null)
            .ShouldBe(new TextAction(SecretText.Empty, TextMethod.Unicode));
        ActionKinds.Switch(tap, ActionKind.Macro, null).ShouldBe(new MacroAction([]));
        ActionKinds
            .Switch(tap, ActionKind.Url, null)
            .ShouldBe(new UrlAction(new UrlTarget.Raw("")));
        ActionKinds
            .Switch(tap, ActionKind.App, null)
            .ShouldBe(new AppAction(new AppTarget.Raw("")));
    }

    [Fact]
    [Trait("Req", "EDI-006")]
    public void Leaving_mouse_drops_the_mouse_action_and_what_the_kind_had_comes_back()
    {
        var mouse = new MouseAction(MouseOp.DoubleClick, ScrollSpeed.Fast);

        var tap = ActionKinds.Switch(mouse, ActionKind.Tap, null);

        tap.ShouldBe(new TapAction(KeyChord.Empty, []));
        ActionKinds.Switch(tap, ActionKind.Mouse, mouse).ShouldBe(mouse);
        ActionKinds
            .Switch(mouse, ActionKind.Toggle, new TapAction(CtrlC, []))
            .ShouldBe(new ToggleAction(CtrlC));
    }

    [Theory]
    [Trait("Req", "EDI-014")]
    [InlineData("ejemplo.com", "https://ejemplo.com/")]
    [InlineData("http://ejemplo.com/a?b=1#c", "http://ejemplo.com/a?b=1#c")]
    [InlineData("España.es", "https://españa.es/")]
    [InlineData("localhost:8080", "https://localhost:8080/")]
    [InlineData("192.168.1.10/panel", "https://192.168.1.10/panel")]
    [InlineData("[::1]:5000", "https://[::1]:5000/")]
    public void Web_accepts_http_and_https_with_ñ_localhost_ips_ports_and_parameters(
        string text,
        string address
    ) =>
        Targets
            .ParseUrl(text)
            .ShouldBeOfType<UrlTarget.Valid>()
            .Address.AbsoluteUri.ShouldBe(address);

    [Theory]
    [Trait("Req", "EDI-014")]
    [InlineData("")]
    [InlineData("ejemplo")]
    [InlineData("ftp://ejemplo.com")]
    [InlineData("javascript:alert(1)")]
    [InlineData("hola mundo.com")]
    [InlineData("ejemplo.")]
    public void Anything_else_is_kept_as_written_and_incomplete(string text) =>
        Targets.ParseUrl(text).ShouldBe(new UrlTarget.Raw(text));

    [Fact]
    [Trait("Req", "EDI-014")]
    [Trait("Req", "EJE-011")]
    public void App_targets_are_executables_with_their_arguments_store_apps_or_documents()
    {
        Targets.ParseApp("notepad.exe").ShouldBe(new AppTarget.Executable("notepad.exe", ""));
        Targets
            .ParseApp("\"C:\\Program Files\\App\\app.exe\" --nuevo")
            .ShouldBe(new AppTarget.Executable("C:\\Program Files\\App\\app.exe", "--nuevo"));
        Targets
            .ParseApp("code.exe C:\\proyecto")
            .ShouldBe(new AppTarget.Executable("code.exe", "C:\\proyecto"));
        Targets
            .ParseApp("shell:AppsFolder\\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App")
            .ShouldBe(new AppTarget.StoreApp("Microsoft.WindowsCalculator_8wekyb3d8bbwe!App"));
        Targets
            .ParseApp("C:\\Docs\\informe.docx")
            .ShouldBe(new AppTarget.Document("C:\\Docs\\informe.docx"));
        Targets
            .ParseApp("\\\\servidor\\app.exe")
            .ShouldBeOfType<AppTarget.Raw>("UNC paths need a confirmation");
        Targets.ParseApp("  ").ShouldBeOfType<AppTarget.Raw>();
    }

    [Fact]
    [Trait("Req", "EDI-014")]
    public void The_fields_show_the_targets_again() =>
        Targets
            .Text(Targets.ParseApp("\"C:\\Program Files\\App\\app.exe\" --nuevo"))
            .ShouldBe("\"C:\\Program Files\\App\\app.exe\" --nuevo");

    [Fact]
    [Trait("Req", "EDI-007")]
    [Trait("Req", "EJE-014")]
    public void Blocked_and_special_combinations_are_warned_whatever_the_order_and_the_side()
    {
        ComboWarnings
            .Of(KeyChord.FromKeys(KeyIds.Delete, KeyIds.LeftCtrl, KeyIds.Alt))
            .ShouldBe(ComboWarning.Blocked);
        ComboWarnings.Of(KeyChord.FromKeys(KeyIds.Win, KeyIds.L)).ShouldBe(ComboWarning.Blocked);
        ComboWarnings.Of(KeyChord.FromKeys(KeyIds.Alt, KeyIds.Tab)).ShouldBe(ComboWarning.Special);
        ComboWarnings
            .Of(KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.Shift, KeyIds.Escape))
            .ShouldBe(ComboWarning.Special);
        ComboWarnings.Of(CtrlC).ShouldBe(ComboWarning.None);
        ComboWarnings.Of(KeyChord.Empty).ShouldBe(ComboWarning.None);
    }

    [Fact]
    [Trait("Req", "EDI-007")]
    public void The_warnings_are_the_combinations_of_the_catalog()
    {
        using var json = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RepoPaths.Data, "catalogs", "blocked-combos.json"))
        );
        var combos = json.RootElement.GetProperty("combos").EnumerateArray().ToList();

        Keys(combos, "blocked")
            .ShouldBe(ComboWarnings.BlockedKeys.Select(k => string.Join('+', k)));
        Keys(combos, "special")
            .ShouldBe(ComboWarnings.SpecialKeys.Select(k => string.Join('+', k)));

        static IEnumerable<string> Keys(List<JsonElement> combos, string level) =>
            combos
                .Where(c =>
                    string.Equals(
                        c.GetProperty("level").GetString(),
                        level,
                        StringComparison.Ordinal
                    )
                )
                .Select(c =>
                    string.Join(
                        '+',
                        c.GetProperty("keys").EnumerateArray().Select(k => k.GetString())
                    )
                );
    }

    [Theory]
    [Trait("Req", "EDI-016")]
    [InlineData(PositionMove.First, 2, 5, 0)]
    [InlineData(PositionMove.Before, 2, 5, 1)]
    [InlineData(PositionMove.After, 2, 5, 3)]
    [InlineData(PositionMove.Last, 2, 5, 4)]
    [InlineData(PositionMove.First, 0, 5, null)]
    [InlineData(PositionMove.Before, 0, 5, null)]
    [InlineData(PositionMove.After, 4, 5, null)]
    [InlineData(PositionMove.Last, 4, 5, null)]
    [InlineData(PositionMove.After, 0, 1, null)]
    public void Position_buttons_that_would_not_move_are_disabled(
        PositionMove move,
        int index,
        int count,
        int? target
    ) => Positions.Target(move, index, count).ShouldBe(target);

    [Fact]
    [Trait("Req", "EDI-013")]
    public void Macro_steps_are_added_moved_and_waits_stay_in_range()
    {
        var macro = MacroSteps.Add(new MacroAction([]), MacroStepKind.Keys);
        macro = MacroSteps.Add(macro, MacroStepKind.Wait);
        macro = MacroSteps.Add(macro, MacroStepKind.Text);
        macro = MacroSteps.Add(macro, MacroStepKind.Mouse);

        macro.Steps[1].ShouldBe(new WaitStep(Timings.Macro.MacroWaitDefault));
        macro.Steps[3].ShouldBe(new MouseStep(MouseOp.RightClick));
        MacroSteps.Move(macro, 0, -1).ShouldBe(macro, "the first step cannot go up");
        MacroSteps.Move(macro, 0, 1).Steps[0].ShouldBeOfType<WaitStep>();
        Wait(MacroSteps.Nudge(macro, 1, 1)).ShouldBe(TimeSpan.FromMilliseconds(600));
        Wait(MacroSteps.Nudge(macro, 1, -100)).ShouldBe(Timings.Macro.MacroWaitRange.Min);
        Wait(MacroSteps.Nudge(macro, 1, 1000)).ShouldBe(Timings.Macro.MacroWaitRange.Max);
        MacroSteps
            .EditKeys(macro, 0, c => ChordEdits.Tap(c, KeyIds.F5))
            .Steps[0]
            .ShouldBe(new KeysStep(KeyChord.FromKeys(KeyIds.F5)));
        MacroSteps.SetMouse(macro, 3, MouseOp.Drag).Steps[3].ShouldBe(new MouseStep(MouseOp.Drag));
        MacroSteps.SetMouse(macro, 1, MouseOp.Drag).ShouldBe(macro, "a wait is not a mouse step");

        static TimeSpan Wait(MacroAction macro) => ((WaitStep)macro.Steps[1]).Duration;
    }
}
