using Clicalo.Application.UseCases.Editor;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Privacy;

namespace Clicalo.Application.Tests.UseCases.Editor;

/// <summary>The «Ver» animation of the «Probar» card by kind (PRB-002), with the times of the catalog.</summary>
[Trait("Req", "PRB-002")]
public sealed class TryPlaybackTests
{
    private static readonly KeyChord CtrlShiftS = KeyChord.FromKeys(
        KeyIds.Ctrl,
        KeyIds.Shift,
        KeyIds.S
    );

    [Fact]
    public void Press_lights_one_key_every_260_ms_and_releases_all_500_ms_later() =>
        Frames(new TapAction(CtrlShiftS, []))
            .ShouldBe([
                (0, 1, PlaybackPhase.Pressing),
                (260, 2, PlaybackPhase.Pressing),
                (520, 3, PlaybackPhase.Pressing),
                (1020, 0, PlaybackPhase.ReleasedAll),
            ]);

    [Fact]
    public void Hold_touches_holds_lifts_and_ends() =>
        Frames(new HoldAction(KeyChord.FromKeys(KeyIds.Win, KeyIds.H)))
            .ShouldBe([
                (0, 0, PlaybackPhase.Touch),
                (0, 1, PlaybackPhase.Touch),
                (260, 2, PlaybackPhase.Touch),
                (460, 2, PlaybackPhase.Holding),
                (2060, 0, PlaybackPhase.Lift),
                (2960, 0, PlaybackPhase.Done),
            ]);

    [Fact]
    public void Toggle_latches_with_the_first_tap_and_releases_with_the_second() =>
        Frames(new ToggleAction(KeyChord.FromKeys(KeyIds.Shift)))
            .ShouldBe([
                (0, 0, PlaybackPhase.FirstTap),
                (0, 1, PlaybackPhase.FirstTap),
                (200, 1, PlaybackPhase.Latched),
                (1800, 1, PlaybackPhase.SecondTap),
                (2300, 0, PlaybackPhase.Released),
            ]);

    [Fact]
    public void Macro_shows_each_step_every_650_ms_and_ends_700_ms_later()
    {
        var macro = new MacroAction([
            new KeysStep(KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.C)),
            new WaitStep(TimeSpan.FromMilliseconds(500)),
        ]);

        Frames(macro)
            .ShouldBe([
                (0, 1, PlaybackPhase.Step),
                (650, 2, PlaybackPhase.Step),
                (1350, 2, PlaybackPhase.Done),
            ]);
    }

    [Fact]
    public void Text_web_app_and_mouse_show_their_sentence_and_end_1100_ms_later() =>
        Frames(new TextAction(SecretText.From("hola"), TextMethod.Unicode))
            .ShouldBe([(0, 1, PlaybackPhase.Sentence), (1100, 1, PlaybackPhase.Done)]);

    [Fact]
    public void Reduce_motion_jumps_to_the_end() =>
        TryPlayback
            .Script(new HoldAction(CtrlShiftS), reduceMotion: true)
            .ShouldHaveSingleItem()
            .ShouldBe(new PlaybackFrame(TimeSpan.Zero, 0, PlaybackPhase.Done, -1));

    private static List<(int, int, PlaybackPhase)> Frames(ShortcutAction action) =>
        [
            .. TryPlayback
                .Script(action, reduceMotion: false)
                .Select(f => ((int)f.At.TotalMilliseconds, f.Down, f.Phase)),
        ];
}
