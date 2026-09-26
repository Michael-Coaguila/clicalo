using Clicalo.Domain.Keys;
using Clicalo.Infrastructure.Persistence.Mappers;

namespace Clicalo.Infrastructure.Tests.Persistence;

/// <summary>Persisted key strokes: canonical ids with an optional side, in press order (EJE-003, EDI-009).</summary>
[Trait("Req", "EDI-009")]
public sealed class KeyStrokeFormatTests
{
    [Theory]
    [InlineData("ctrl", "ctrl", KeySide.Any)]
    [InlineData("ctrl@left", "ctrl", KeySide.Left)]
    [InlineData("shift@right", "shift", KeySide.Right)]
    [InlineData("num.add", "num.add", KeySide.Any)]
    [InlineData("char:ñ", "char:ñ", KeySide.Any)]
    [InlineData("char:@", "char:@", KeySide.Any)]
    [InlineData("char:@@left", "char:@", KeySide.Left)]
    public void Reads_and_writes_the_same_text(string text, string key, KeySide side)
    {
        KeyStrokeFormat.TryParse(text, out var stroke).ShouldBeTrue();

        stroke.ShouldBe(new KeyStroke(new KeyId(key), side));
        KeyStrokeFormat.Format(stroke).ShouldBe(text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Blank_text_is_not_a_stroke(string? text) =>
        KeyStrokeFormat.TryParse(text, out _).ShouldBeFalse();
}
