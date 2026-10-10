using System.Text.Json;
using Clicalo.Application.Tests.Localization;
using Clicalo.Application.Tests.Store;
using Clicalo.Application.UseCases.Editor;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Primitives;
using Clicalo.TestKit;

namespace Clicalo.Application.Tests.UseCases.Editor;

/// <summary>
/// «Grabar con teclado» (EDI-010) and the alternative of a blocked combination (EDI-007) as rules: the modifiers in the
/// order they were pressed and with their side, the first other key closes, Esc cancels, and Win+L offers the system
/// action that replaces it.
/// </summary>
public sealed class ChordRecorderTests
{
    private static readonly KeyId LeftCtrl = new("lctrl");
    private static readonly KeyId RightShift = new("rshift");

    [Fact]
    [Trait("Req", "EDI-010")]
    public void Modifiers_alone_do_not_end_the_recording_and_the_first_other_key_closes_it()
    {
        var recorder = new ChordRecorder();

        recorder.Down(RightShift).ShouldBeOfType<ChordRecording.Waiting>();
        recorder.Down(LeftCtrl).ShouldBeOfType<ChordRecording.Waiting>();
        var recorded = recorder.Down(KeyIds.B).ShouldBeOfType<ChordRecording.Recorded>();

        // In the order they were pressed (Shift, then Ctrl), each with its side.
        recorded
            .Chord.Strokes.Select(s => (s.Key, s.Side))
            .ShouldBe([
                (KeyIds.Shift, KeySide.Right),
                (KeyIds.Ctrl, KeySide.Left),
                (KeyIds.B, KeySide.Any),
            ]);
    }

    [Fact]
    [Trait("Req", "EDI-010")]
    public void A_released_modifier_and_a_repeated_one_do_not_count()
    {
        var recorder = new ChordRecorder();

        _ = recorder.Down(LeftCtrl);
        _ = recorder.Down(LeftCtrl);
        _ = recorder.Down(RightShift);
        recorder.Up(RightShift);
        var recorded = recorder.Down(KeyIds.C).ShouldBeOfType<ChordRecording.Recorded>();

        recorded.Chord.Strokes.Select(s => s.Key).ShouldBe([KeyIds.Ctrl, KeyIds.C]);
    }

    [Fact]
    [Trait("Req", "EDI-010")]
    public void Esc_cancels_and_an_unknown_key_is_ignored()
    {
        var recorder = new ChordRecorder();

        recorder.Down(new KeyId("not-a-key")).ShouldBeOfType<ChordRecording.Waiting>();
        _ = recorder.Down(LeftCtrl);
        recorder.Down(KeyIds.Escape).ShouldBeOfType<ChordRecording.Cancelled>();

        // Nothing is left from the cancelled recording.
        recorder
            .Down(KeyIds.F5)
            .ShouldBeOfType<ChordRecording.Recorded>()
            .Chord.Strokes.Select(s => s.Key)
            .ShouldBe([KeyIds.F5]);
    }

    [Fact]
    [Trait("Req", "EDI-010")]
    [Trait("Req", "IDI-004")]
    public void A_recorded_combination_replaces_the_one_of_the_box_as_one_undo_step_with_its_notice()
    {
        var harness = new StoreHarness(StoreSamples.Document());
        var localization = I18nRepository.Context("es");
        var workspace = new ShortcutsWorkspace(
            harness.Store,
            localization,
            () => EditorCatalogs.Empty,
            () => ProfileId.General
        );
        var notices = new List<WorkspaceNotice>();
        workspace.Noticed += (_, e) => notices.Add(e.Notice);
        workspace.Select(StoreSamples.Bold);

        workspace.RecordChord(KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.Shift, KeyIds.B));

        workspace
            .ChordInBox()
            .ShouldNotBeNull()
            .Strokes.Select(s => s.Key)
            .ShouldBe([KeyIds.Ctrl, KeyIds.Shift, KeyIds.B]);
        var notice = notices[^1];
        notice.CanUndo.ShouldBeTrue();
        localization.Current.Format(notice.Text).ShouldStartWith("Combinación grabada: ");
        harness.Store.Undo().IsSuccess.ShouldBeTrue();
        workspace
            .ChordInBox()
            .ShouldNotBeNull()
            .Strokes.Select(s => s.Key)
            .ShouldBe([KeyIds.Ctrl, KeyIds.N]);
    }

    [Fact]
    [Trait("Req", "EDI-007")]
    [Trait("Req", "EJE-014")]
    public void The_blocked_combination_with_a_system_alternative_is_the_one_of_the_catalog()
    {
        ComboWarnings
            .AlternativeOf(KeyChord.FromKeys(KeyIds.Win, KeyIds.L))
            .ShouldBe(ComboWarnings.LockCommand);
        ComboWarnings
            .AlternativeOf(KeyChord.FromKeys(KeyIds.L, new KeyId("rwin")))
            .ShouldBe(ComboWarnings.LockCommand, "whatever the order and the side");
        ComboWarnings
            .AlternativeOf(KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.Alt, KeyIds.Delete))
            .ShouldBeNull("blocked, with no alternative");
        ComboWarnings.AlternativeOf(KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.C)).ShouldBeNull();

        using var json = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RepoPaths.Data, "catalogs", "blocked-combos.json"))
        );
        json.RootElement.GetProperty("combos")
            .EnumerateArray()
            .Where(c => c.TryGetProperty("alternative", out _))
            .Select(c =>
                string.Join('+', c.GetProperty("keys").EnumerateArray().Select(k => k.GetString()))
                + "="
                + c.GetProperty("alternative").GetProperty("systemCommand").GetString()
            )
            .ShouldBe(
                ComboWarnings.Alternatives.Select(a =>
                    string.Join('+', a.Keys) + "=" + a.SystemCommand
                )
            );
    }
}
