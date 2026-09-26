using Clicalo.Infrastructure.Migration;

namespace Clicalo.Infrastructure.Tests.Migration;

/// <summary>The zip limits come from timings.json and match the table of blueprint §6.6.</summary>
[Trait("Req", "MIG-009")]
public sealed class SafeZipLimitsTests
{
    private const long MiB = 1024 * 1024;

    [Fact]
    public void The_default_limits_are_those_of_the_blueprint()
    {
        var limits = SafeZipLimits.Default;

        limits.MaxTotalBytes.ShouldBe(50 * MiB);
        limits.MaxEntries.ShouldBe(1000);
        limits.MaxCompressionRatio.ShouldBe(100);
        limits.MaxJsonBytes.ShouldBe(5 * MiB);
    }
}
