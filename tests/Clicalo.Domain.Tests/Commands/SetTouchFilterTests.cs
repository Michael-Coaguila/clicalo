using Clicalo.Domain.Catalog;
using Clicalo.Domain.Commands;
using Clicalo.Domain.Document;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Settings;
using Clicalo.Domain.Tests.Generators;
using static Clicalo.Domain.Tests.Library.LibraryBuilder;

namespace Clicalo.Domain.Tests.Commands;

/// <summary>The touch filter written as a whole: one undo step for a preset or a slider (TAC-001, TAC-005).</summary>
public sealed class SetTouchFilterTests
{
    private static readonly UserDocument Document = UserDocument.Create(
        Sample(),
        SettingsSchema.Defaults
    );

    [Fact]
    [Trait("Req", "TAC-001")]
    [Trait("Req", "REG-07")]
    public void A_preset_writes_its_four_values_and_its_id_in_one_undo_step()
    {
        var strong = TouchPresets.Find("strong-tremor")!;
        var touch = new TouchFilterSettings(
            strong.Id,
            strong.Debounce,
            strong.HitSlopPx,
            strong.CancelMovePx,
            strong.MinContact
        );

        var change = new SetTouchFilter(touch).Apply(Document, Contexts.Fresh()).Value;

        change.Next.Settings.Touch.ShouldBe(touch);
        change.Next.Settings.Touch.CancelMovePx.ShouldBe(
            28,
            "a preset value off the step is kept exact"
        );
        change.Undo.ShouldBe(new UndoIntent.Record(L.TouchTitle.Key, SetTouchFilter.CoalesceKey));
    }

    [Fact]
    [Trait("Req", "TAC-005")]
    public void Every_value_must_be_inside_its_range()
    {
        var touch = Document.Settings.Touch;

        Fail(touch with { HitSlopPx = 41 }).Code.ShouldBe("command.setting.out_of_range");
        Fail(touch with { Debounce = TimeSpan.FromMilliseconds(1001) })
            .Code.ShouldBe("command.setting.out_of_range");
        Fail(touch with { Preset = string.Empty }).Code.ShouldBe("command.setting.out_of_range");
        new SetTouchFilter(
            touch with
            {
                Preset = SettingsSchema.PersonalTouchPreset,
                CancelMovePx = 0,
            }
        )
            .Apply(Document, Contexts.Fresh())
            .Value.Next.Settings.Touch.CancelMovePx.ShouldBe(0, "zero switches the check off");
    }

    private static Errors.Failure Fail(TouchFilterSettings touch)
    {
        var result = new SetTouchFilter(touch).Apply(Document, Contexts.Fresh());
        result.IsFailure.ShouldBeTrue();
        return result.Failure;
    }
}
