using Clicalo.Tools.SpikeLab.Measurement;

namespace Clicalo.Tools.SpikeLab.Tests.Measurement;

public sealed class ProbeSignalsTests
{
    [Theory]
    [InlineData(0x0100u, 0x87ul, (ushort)0x87, "F24")]
    [InlineData(0x0104u, 0x87ul, (ushort)0x87, "F24")]
    [InlineData(0x0101u, 0x87ul, (ushort)0x87, "None")]
    [InlineData(0x0100u, 0x11ul, (ushort)0x11, "ModifierKey")]
    [InlineData(0x0105u, 0x12ul, (ushort)0x12, "ModifierKey")]
    [InlineData(0x0100u, 0xA2ul, (ushort)0xA2, "ModifierKey")]
    [InlineData(0x0100u, 0x5Bul, (ushort)0x5B, "ModifierKey")]
    [InlineData(0x0100u, 0x41ul, (ushort)0x41, "None")]
    [InlineData(0x0102u, 0x61ul, (ushort)0, "Character")]
    [InlineData(0x0106u, 0x61ul, (ushort)0, "Character")]
    [InlineData(0x0109u, 0x61ul, (ushort)0, "Character")]
    [InlineData(0x0112u, 0xF100ul, (ushort)0, "Menu")]
    [InlineData(0x0112u, 0xF10Aul, (ushort)0, "Menu")]
    [InlineData(0x0112u, 0xF020ul, (ushort)0, "None")]
    [InlineData(0x0006u, 0x1ul, (ushort)0, "None")]
    public void Messages_are_classified_for_the_S4_counters(
        uint message,
        ulong wParam,
        ushort virtualKey,
        string expected
    ) =>
        ProbeSignals
            .Classify(message, wParam, virtualKey)
            .ShouldBe(Enum.Parse<ProbeSignal>(expected));
}
