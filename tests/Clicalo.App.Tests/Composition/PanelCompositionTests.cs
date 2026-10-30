using Clicalo.App.Composition;
using Clicalo.Application.Coordinators;
using Clicalo.Application.Engine;
using Clicalo.Application.Interaction;
using Clicalo.Application.Session;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Messages;
using Clicalo.Domain.ProfileResolution;
using Clicalo.Domain.Settings;
using Clicalo.Domain.Timing;
using Clicalo.Presentation.Panel;

namespace Clicalo.App.Tests.Composition;

/// <summary>
/// The composition of the panel without the executable (<see cref="CompositionWorld"/>): the time of the notices
/// (ACC-006), pause and resume (BUR-004) and the global shortcut of the tray (BUR-005).
/// </summary>
public sealed class PanelCompositionTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [Trait("Req", "ACC-006")]
    [Trait("Req", "AVI-002")]
    public void A_notice_lasts_its_time_by_the_multiplier_once(int multiplier) =>
        UiThread.Run(() =>
        {
            var world = new CompositionWorld();
            world.Set(SettingPaths.TimeMultiplier, multiplier);
            var lasts = TimeSpan.FromTicks(Timings.Notices.NoticeDuration.Ticks * multiplier);
            var start = world.Time.GetUtcNow();

            world.Panel.Notify(L.Deleted, NoticeTone.Notice, "delete");

            world.Interaction.Current.Notices.EndsAt.ShouldBe(start + lasts);
            world.Panel.CurrentNotice.ShouldNotBeNull().Text.ShouldBe(L.Deleted);

            // Not a moment less and not the multiplier twice: it is on show until its time and gone right after.
            world.Time.Advance(lasts - TimeSpan.FromMilliseconds(1));
            UiThread.Drain();
            world.Panel.CurrentNotice.ShouldNotBeNull();
            world.Time.Advance(TimeSpan.FromMilliseconds(1));
            UiThread.Drain();
            world.Panel.CurrentNotice.ShouldBeNull();
        });

    [Fact]
    [Trait("Req", "ACC-006")]
    [Trait("Req", "AVI-003")]
    public void A_notice_with_undo_lasts_the_undo_time_by_the_multiplier() =>
        UiThread.Run(() =>
        {
            var world = new CompositionWorld();
            world.Set(SettingPaths.TimeMultiplier, 3);
            // Something to undo, so the notice keeps its [undo].
            world.Set(SettingPaths.VoiceNumbers, !world.Store.Current.Settings.VoiceNumbers);
            world.Store.CanUndo.ShouldBeTrue();
            var start = world.Time.GetUtcNow();

            world.Panel.Notify(L.Deleted, NoticeTone.Notice, "delete", canUndo: true);

            world.Panel.CurrentNotice.ShouldNotBeNull().CanUndo.ShouldBeTrue();
            world.Interaction.Current.Notices.EndsAt.ShouldBe(
                start + TimeSpan.FromTicks(Timings.Notices.UndoNoticeDuration.Ticks * 3)
            );
        });

    [Fact]
    [Trait("Req", "BUR-004")]
    [Trait("Req", "PER-003")]
    public void Pausing_hides_the_panel_and_resuming_follows_the_app_in_front_again() =>
        UiThread.Run(() =>
        {
            var world = new CompositionWorld();
            var monitor = new CompositionWorld.ScriptedMonitor();
            world.Panel.Follow(monitor, CompositionWorld.Describe);
            monitor.Front(CompositionWorld.ScriptedMonitor.WordId);
            world.Profiles.State.View.ShouldBe(new ViewTarget.Profile(CompositionWorld.Word));
            world.Session.Current.Presence.ShouldBe(PanelPresence.Visible);

            world.Panel.Panel.ApplyEngine(EngineSnapshot.Empty with { Paused = true });
            UiThread.Drain();

            world.Session.Current.Presence.ShouldBe(
                PanelPresence.Hidden,
                "paused, the panel hides"
            );
            monitor.Front(CompositionWorld.ScriptedMonitor.ExcelId);
            world.Profiles.State.View.ShouldBe(
                new ViewTarget.Profile(CompositionWorld.Word),
                "paused, the profile does not follow the app"
            );

            world.Panel.Panel.ApplyEngine(EngineSnapshot.Empty);
            UiThread.Drain();

            world.Session.Current.Presence.ShouldBe(PanelPresence.Visible, "the panel comes back");
            world.Profiles.State.View.ShouldBe(
                new ViewTarget.Profile(CompositionWorld.Excel),
                "and catches up with the app that is in front by then"
            );
            world.Foreground.Requests.ShouldBe(0, "none of it asks for the foreground (REG-01)");
        });

    [Fact]
    [Trait("Req", "BUR-005")]
    public void The_global_shortcut_is_registered_changed_and_removed_with_its_setting() =>
        UiThread.Run(() =>
        {
            var world = new CompositionWorld();
            var asked = new List<KeyChord?>();
            world.Panel.AttachHotkey(chord =>
            {
                asked.Add(chord);
                return Task.FromResult(true);
            });
            UiThread.Drain();
            asked.ShouldBe([null], "off by default (D10)");

            world.Set(SettingPaths.GlobalHotkeyEnabled, true);
            var combo = world.Store.Current.Settings.GlobalHotkey.Combo;
            asked.Count.ShouldBe(2);
            asked[^1].ShouldBe(GlobalHotkeys.Find(combo).ShouldNotBeNull().Keys);

            var other = GlobalHotkeys.All.First(hotkey =>
                !string.Equals(hotkey.Id, combo, StringComparison.Ordinal)
            );
            world.Set(SettingPaths.GlobalHotkeyCombo, other.Id);
            asked.Count.ShouldBe(3);
            asked[^1].ShouldBe(other.Keys);

            // Another setting leaves the registration alone.
            world.Set(SettingPaths.VoiceNumbers, !world.Store.Current.Settings.VoiceNumbers);
            asked.Count.ShouldBe(3);

            world.Set(SettingPaths.GlobalHotkeyEnabled, false);
            asked.Count.ShouldBe(4);
            asked[^1].ShouldBeNull("turned off, the combination is free again");
            world.Panel.CurrentNotice.ShouldBeNull("nothing to say while Windows accepts it");
        });

    [Fact]
    [Trait("Req", "BUR-005")]
    public void A_combination_another_program_owns_is_said_in_the_panel() =>
        UiThread.Run(() =>
        {
            var world = new CompositionWorld();
            world.Panel.AttachHotkey(static chord => Task.FromResult(chord is null));
            UiThread.Drain();
            world.Panel.CurrentNotice.ShouldBeNull();

            world.Set(SettingPaths.GlobalHotkeyEnabled, true);

            var notice = world.Panel.CurrentNotice.ShouldNotBeNull();
            notice.Tone.ShouldBe(NoticeTone.Warning);
            world
                .Text(notice.Text)
                .ShouldStartWith(world.Text(L.GlobalHotkeyTaken(keys: string.Empty))[..8]);
        });

    [Fact]
    [Trait("Req", "BUR-005")]
    [Trait("Req", "BUR-003")]
    [Trait("Req", "REG-01")]
    public void The_shortcut_and_the_tray_show_the_panel_without_asking_for_the_foreground() =>
        UiThread.Run(() =>
        {
            var world = new CompositionWorld();
            var visibility = new PanelVisibilityCoordinator(world.Session, world.Engine);
            visibility.Hide();
            world.Session.Current.Presence.ShouldBe(PanelPresence.Hidden);
            world.Engine.Posted.Clear();

            PanelLinks.ToggleFromTray(visibility, world.Interaction);

            world.Session.Current.Presence.ShouldBe(PanelPresence.Visible);
            world.Foreground.Requests.ShouldBe(0, "the panel shows where it was, passively");
            world.Engine.Posted.ShouldBeEmpty("showing sends nothing");

            // BUR-003: with the bubble on screen, the panel comes back instead of hiding.
            world.Interaction.Dispatch(new InteractionAction.Minimize()).ShouldBeTrue();
            PanelLinks.ToggleFromTray(visibility, world.Interaction);
            world.Interaction.Current.Minimized.ShouldBeFalse();
            world.Session.Current.Presence.ShouldBe(PanelPresence.Visible);

            // And on the panel in view it hides it, releasing what is held (SEG-007).
            PanelLinks.ToggleFromTray(visibility, world.Interaction);
            world.Session.Current.Presence.ShouldBe(PanelPresence.Hidden);
            world
                .Engine.Posted.ShouldHaveSingleItem()
                .ShouldBe(new EngineEvent.Terminal(TerminalReason.Hide));
            world.Foreground.Requests.ShouldBe(0);
        });
}
