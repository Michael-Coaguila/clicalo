using Clicalo.Domain.Execution;
using Clicalo.Domain.Library;
using Clicalo.Domain.Tests.Execution.Support;

namespace Clicalo.Domain.Tests.Execution;

/// <summary>
/// Releases the secure desktop refuses without locking the session (UAC, Ctrl+Alt+Del; blueprint §7.6, INV-3, D-22):
/// «Release all» and every terminal event send them again, the return of the input desktop sends them again, and a
/// key pressed again since is never released under its new holder.
/// </summary>
[Trait("Req", "SEG-006")]
[Trait("Req", "SEG-003")]
[Trait("Req", "REG-03")]
public sealed class BlockedReleasesTests
{
    public static TheoryData<string> WaysToReleaseEverything =>
        ["release all", "app switch release", "hide", "view change", "pause", "test mode", "exit"];

    private static EngineEvent ReleaseEverything(string way) =>
        way switch
        {
            "release all" => new EngineEvent.ReleaseAll(ReleaseReason.User),
            "app switch release" => new EngineEvent.ReleaseAll(ReleaseReason.AppSwitch),
            "hide" => new EngineEvent.Terminal(TerminalReason.Hide),
            "view change" => new EngineEvent.Terminal(TerminalReason.ViewChange),
            "pause" => new EngineEvent.SetPaused(true),
            "test mode" => new EngineEvent.SetTestMode(true),
            _ => new EngineEvent.Terminal(TerminalReason.Exit),
        };

    /// <summary>A latched Ctrl whose release the secure desktop refused: Ctrl is still down, no holder knows it.</summary>
    private static EngineHarness CtrlLeftDownByUac()
    {
        var engine = new EngineHarness();
        engine.Foreground();
        engine.Invoke(Shortcuts.Toggle("lctrl", "ctrl"));
        engine.Settle();
        engine.Receiver.Keys.Count.ShouldBe(1);

        // The UAC prompt comes up and an app switch releases everything while it is in front.
        engine.RefuseReleases = true;
        engine.Apply(new EngineEvent.ReleaseAll(ReleaseReason.AppSwitch));
        engine.RefuseReleases = false;

        engine.State.Keys.IsEmpty.ShouldBeTrue();
        engine.State.BlockedReleases.IsEmpty.ShouldBeFalse();
        engine.Receiver.Keys.Count.ShouldBe(1);
        return engine;
    }

    [Theory]
    [MemberData(nameof(WaysToReleaseEverything))]
    public void Releasing_everything_after_the_prompt_closed_lets_go_of_what_it_refused(string way)
    {
        var engine = CtrlLeftDownByUac();

        engine.Apply(ReleaseEverything(way));

        engine.Receiver.IsEmpty.ShouldBeTrue();
        engine.Receiver.Anomalies.ShouldBeEmpty();
        engine.State.BlockedReleases.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void The_input_desktop_coming_back_lets_go_of_what_it_refused()
    {
        var engine = CtrlLeftDownByUac();

        var effects = engine.Apply(new EngineEvent.SessionResumed());

        engine.Receiver.IsEmpty.ShouldBeTrue();
        engine.State.BlockedReleases.IsEmpty.ShouldBeTrue();
        effects.OfType<EngineEffect.Inject>().ShouldAllBe(static inject => inject.IsRelease);
    }

    [Fact]
    [Trait("Req", "SEG-004")]
    public void A_deadline_that_falls_while_the_prompt_is_up_is_sent_again_when_it_closes()
    {
        var engine = new EngineHarness();
        engine.Foreground();
        engine.Invoke(
            Shortcuts.Of(
                "hold2s",
                new HoldAction(Chords.Of("ctrl", "shift")),
                Shortcuts.Limited(TimeSpan.FromSeconds(2))
            )
        );
        engine.Settle();
        engine.Receiver.Keys.Count.ShouldBe(2);

        engine.RefuseReleases = true;
        engine.Advance(TimeSpan.FromSeconds(3));
        engine.RefuseReleases = false;
        engine.Receiver.Keys.Count.ShouldBe(2);

        engine.Apply(new EngineEvent.SessionResumed());

        engine.Receiver.IsEmpty.ShouldBeTrue();
        engine.Receiver.Anomalies.ShouldBeEmpty();
    }

    [Fact]
    public void A_refused_release_never_lets_go_of_a_key_a_holder_pressed_again_since()
    {
        var engine = CtrlLeftDownByUac();
        engine.Invoke(Shortcuts.Toggle("lctrl", "ctrl"));
        engine.Settle();
        var ctrl = engine.State.Keys.Holders.Keys.ShouldHaveSingleItem();

        engine.Apply(new EngineEvent.SessionResumed());

        // The toggle holds Ctrl and Ctrl is down: its own release lets go of it.
        engine.State.Keys.IsDown(ctrl).ShouldBeTrue();
        engine.Receiver.Keys.ShouldContain(ctrl);
        engine.Apply(new EngineEvent.ReleaseAll(ReleaseReason.User));
        engine.Receiver.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Releases_refused_again_are_kept_again()
    {
        var engine = CtrlLeftDownByUac();

        engine.RefuseReleases = true;
        engine.Apply(new EngineEvent.ReleaseAll(ReleaseReason.User));
        engine.RefuseReleases = false;

        engine.State.BlockedReleases.Items.ShouldAllBe(static e => e.IsRelease);
        engine.State.BlockedReleases.IsEmpty.ShouldBeFalse();
        engine.Apply(new EngineEvent.SessionResumed());
        engine.Receiver.IsEmpty.ShouldBeTrue();
        engine.State.BlockedReleases.IsEmpty.ShouldBeTrue();
    }
}
