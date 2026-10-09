using Clicalo.Domain.Execution;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Tests.Execution.Support;

namespace Clicalo.Domain.Tests.Execution;

/// <summary>
/// What follows an action (EJE-012): its notice (EJE-003, EJE-008 to EJE-011), Frequents and Repeat (AVI-004), the
/// soft sound; and the actions the engine replaces or refuses before sending (EJE-014, EJE-016, LOG-008).
/// </summary>
public sealed class ActionFeedbackTests
{
    private static EngineHarness Engine(bool sound = false)
    {
        var engine = new EngineHarness(
            EngineHarness.DefaultConfig with
            {
                InterEventDelay = TimeSpan.Zero,
                FeedbackSound = sound,
            }
        );
        engine.Foreground("winword.exe");
        return engine;
    }

    private static List<Message> Notices(IEnumerable<EngineEffect> effects) =>
        [.. effects.OfType<EngineEffect.Notice>().Select(static n => n.Text)];

    private static string Text(Message message, string argument)
    {
        message.TryGetArgument(argument, out var value).ShouldBeTrue();
        value.TryGetText(out var text).ShouldBeTrue();
        return text;
    }

    [Fact]
    [Trait("Req", "EJE-003")]
    public void A_tap_says_which_keys_went_to_which_app()
    {
        var engine = Engine();

        var notice = Notices(engine.Tap(Shortcuts.Tap("save", "ctrl", "s"))).ShouldHaveSingleItem();

        notice.Key.Value.ShouldBe("tapSent");
        Text(notice, "app").ShouldBe("winword");
        Text(notice, "keys").ShouldContain("+");
    }

    [Fact]
    [Trait("Req", "EJE-004")]
    [Trait("Req", "EJE-007")]
    public void A_hold_names_its_keys_and_a_toggle_or_a_drag_its_name()
    {
        var engine = Engine();

        var hold = Notices(engine.Press(Shortcuts.Hold("alt", "alt"), contact: 1))
            .ShouldHaveSingleItem();
        engine.Lift(contact: 1);
        var toggle = Notices(engine.Tap(Shortcuts.Toggle("shift", "shift"), contact: 2))
            .ShouldHaveSingleItem();
        var drag = Notices(engine.Tap(Shortcuts.Mouse("drag", MouseOp.Drag), contact: 3))
            .ShouldHaveSingleItem();

        hold.Key.Value.ShouldBe("holdingKeys");
        Text(hold, "keys").ShouldNotBeNullOrWhiteSpace();
        toggle.Key.Value.ShouldBe("latchedName");
        drag.Key.Value.ShouldBe("latchedName");
    }

    [Fact]
    [Trait("Req", "AVI-004")]
    [Trait("Req", "FRE-002")]
    public void Hold_and_toggle_count_for_frequents_but_never_become_the_action_to_repeat()
    {
        var engine = Engine();

        engine.Tap(Shortcuts.Toggle("shift", "shift"), contact: 1);
        engine.Press(Shortcuts.Hold("alt", "alt"), contact: 2);
        engine.Lift(contact: 2);
        engine.Tap(Shortcuts.Mouse("drag", MouseOp.Drag), contact: 3);

        engine.Effects.OfType<EngineEffect.CountUsage>().Count().ShouldBe(3);
        engine.Effects.OfType<EngineEffect.SetLastAction>().ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "AVI-004")]
    public void A_tap_a_text_and_a_click_become_the_action_to_repeat()
    {
        var engine = Engine();

        engine.Tap(Shortcuts.Tap("save", "ctrl", "s"), contact: 1);
        engine.Tap(Shortcuts.Text("sign", "hola"), contact: 2);
        engine.Tap(Shortcuts.Mouse("right", MouseOp.RightClick), contact: 3);

        engine
            .Effects.OfType<EngineEffect.SetLastAction>()
            .Select(static e => e.Shortcut.Value)
            .ShouldBe(["save", "sign", "right"]);
    }

    [Theory]
    [Trait("Req", "EJE-012")]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public void The_soft_sound_follows_each_action_only_when_it_is_on(bool sound, int expected)
    {
        var engine = Engine(sound);

        engine.Tap(Shortcuts.Tap("save", "ctrl", "s"));

        engine.Effects.OfType<EngineEffect.PlayFeedbackSound>().Count().ShouldBe(expected);
    }

    [Fact]
    [Trait("Req", "EJE-014")]
    [Trait("Req", "EJE-016")]
    public void A_tap_of_win_l_locks_the_computer_instead_of_sending_keys()
    {
        var engine = Engine();

        var effects = engine.Tap(Shortcuts.Tap("lock", "win", "l"));

        effects.OfType<EngineEffect.SystemCommand>().Single().Command.Value.ShouldBe("lock");
        engine.Sent.ShouldBeEmpty();
        effects.OfType<EngineEffect.CountUsage>().ShouldHaveSingleItem();
    }

    [Fact]
    [Trait("Req", "EJE-014")]
    [Trait("Req", "EJE-013")]
    public void Win_l_locks_even_with_an_elevated_app_in_front_because_nothing_is_injected()
    {
        var engine = Engine();
        engine.Foreground("taskmgr.exe", ElevationState.TargetElevated);

        var effects = engine.Tap(Shortcuts.Tap("lock", "win", "l"));

        effects.OfType<EngineEffect.SystemCommand>().ShouldHaveSingleItem();
    }

    [Fact]
    [Trait("Req", "EJE-014")]
    public void Ctrl_alt_delete_sends_nothing_and_says_why()
    {
        var engine = Engine();

        var effects = engine.Tap(Shortcuts.Tap("sas", "ctrl", "alt", "delete"));

        engine.Sent.ShouldBeEmpty();
        effects.OfType<EngineEffect.SystemCommand>().ShouldBeEmpty();
        Notices(effects).ShouldHaveSingleItem().ShouldBe(L.BlockedB);
    }

    [Theory]
    [Trait("Req", "EJE-011")]
    [InlineData(@"C:\Windows\System32\cmd.exe")]
    [InlineData("powershell")]
    [InlineData(@"C:\tools\clean.bat")]
    [InlineData(@"\\server\share\tool.exe")]
    public void An_app_that_is_not_safe_to_start_never_leaves_the_engine(string path)
    {
        var engine = Engine();

        var effects = engine.Tap(Shortcuts.App("tool", path));

        effects.OfType<EngineEffect.Launch>().ShouldBeEmpty();
        effects.OfType<EngineEffect.CountUsage>().ShouldBeEmpty();
        Notices(effects).ShouldHaveSingleItem().Key.Value.ShouldBe("launchUnsafe");
    }

    [Fact]
    [Trait("Req", "EJE-011")]
    public void A_network_path_starts_when_its_shortcut_asks_for_confirmation()
    {
        var engine = Engine();
        var shortcut = Shortcuts.Of(
            "share",
            new AppAction(new AppTarget.Executable(@"\\server\share\tool.exe", string.Empty)),
            Shortcuts.Confirming
        );

        engine.Tap(shortcut, contact: 1).OfType<EngineEffect.Launch>().ShouldBeEmpty();
        engine.Advance(TimeSpan.FromMilliseconds(500));
        engine.Tap(shortcut, contact: 2).OfType<EngineEffect.Launch>().ShouldHaveSingleItem();
    }

    [Fact]
    [Trait("Req", "EJE-011")]
    public void A_web_that_opens_names_its_address()
    {
        var engine = Engine();
        var launch = engine
            .Tap(Shortcuts.Url("mail", "https://example.com/inbox"))
            .OfType<EngineEffect.Launch>()
            .Single();

        var notice = Notices(engine.Apply(new EngineEvent.LaunchCompleted(launch.Effect)))
            .ShouldHaveSingleItem();

        notice.Key.Value.ShouldBe("openedName");
        Text(notice, "name").ShouldBe("https://example.com/inbox");
    }

    [Fact]
    [Trait("Req", "EJE-016")]
    public void A_system_command_that_fails_says_so()
    {
        var engine = Engine();
        var command = engine
            .Tap(Shortcuts.System("brighter", "brightness.up"))
            .OfType<EngineEffect.SystemCommand>()
            .Single();

        var notice = Notices(
                engine.Apply(
                    new EngineEvent.SystemCommandCompleted(command.Effect, Succeeded: false)
                )
            )
            .ShouldHaveSingleItem();

        notice.ShouldBe(L.ActionFailed(name: "brighter"));
    }

    [Fact]
    [Trait("Req", "EJE-008")]
    public void A_text_shows_its_first_sixteen_characters_unless_it_is_private()
    {
        var engine = Engine();
        var shown = Notices(
                engine.Tap(Shortcuts.Text("sign", "Saludos,\nAna García López"), contact: 1)
            )
            .ShouldHaveSingleItem();
        var secret = Shortcuts.Text("pin", "1234") with
        {
            Options = Shortcuts.Default with { IsPrivate = true },
        };

        var hidden = Notices(engine.Tap(secret, contact: 2)).ShouldHaveSingleItem();

        shown.ShouldBe(L.TextTyped(name: "Saludos, Ana Gar…"));
        hidden.ShouldBe(L.TextTypedPrivate);
    }

    [Fact]
    [Trait("Req", "EJE-009")]
    public void A_click_names_its_shortcut()
    {
        var engine = Engine();

        var notice = Notices(engine.Tap(Shortcuts.Mouse("right", MouseOp.RightClick)))
            .ShouldHaveSingleItem();

        notice.ShouldBe(L.MouseRan(name: "right"));
    }

    [Fact]
    [Trait("Req", "EJE-010")]
    public void A_macro_says_it_runs_and_how_it_ended()
    {
        var engine = Engine();
        var macro = Shortcuts.Macro(
            "greet",
            new KeysStep(Chords.Of("ctrl", "c")),
            new WaitStep(TimeSpan.FromMilliseconds(200)),
            new KeysStep(Chords.Of("ctrl", "v"))
        );

        var started = Notices(engine.Tap(macro));
        engine.Advance(TimeSpan.FromSeconds(1));

        started.ShouldContain(L.MacroRunning(name: "greet", index: 1, total: 3));
        Notices(engine.Effects).ShouldContain(L.MacroRanName(name: "greet", total: 3));
        engine.Effects.OfType<EngineEffect.SetLastAction>().ShouldHaveSingleItem();
    }

    [Fact]
    [Trait("Req", "EJE-010")]
    public void A_second_tap_cancels_the_macro_and_says_so()
    {
        var engine = Engine();
        var macro = Shortcuts.Macro(
            "slow",
            new WaitStep(TimeSpan.FromSeconds(5)),
            new KeysStep(Chords.Of("ctrl", "v"))
        );
        engine.Tap(macro, contact: 1);
        engine.Advance(TimeSpan.FromMilliseconds(500));

        var cancelled = Notices(engine.Tap(macro, contact: 2));
        engine.Advance(TimeSpan.FromSeconds(6));

        cancelled.ShouldContain(L.MacroCancelled(name: "slow"));
        engine.State.Macro.ShouldBeNull();
        engine.Sent.ShouldBeEmpty();
    }
}
