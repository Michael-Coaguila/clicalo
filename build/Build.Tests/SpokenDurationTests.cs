namespace Clicalo.Build.Tests;

public sealed class SpokenDurationTests
{
    [Theory]
    [InlineData(0, "menos de 1 s")]
    [InlineData(999, "menos de 1 s")]
    [InlineData(-5_000, "menos de 1 s")]
    [InlineData(1_000, "1 s")]
    [InlineData(45_900, "45 s")]
    [InlineData(60_000, "1 min")]
    [InlineData(72_400, "1 min 12 s")]
    [InlineData(3_600_000, "1 h")]
    [InlineData(3_661_000, "1 h 1 min")]
    public void Formats_whole_units_without_decimals_or_colons(
        long milliseconds,
        string expected
    ) => SpokenDuration.Format(TimeSpan.FromMilliseconds(milliseconds)).ShouldBe(expected);
}
