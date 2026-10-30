using System.Runtime.InteropServices;
using Clicalo.Domain.Settings;
using Clicalo.Infrastructure.Updates;

namespace Clicalo.Infrastructure.Tests.Updates;

/// <summary>
/// The feed a copy of Clícalo reads (ACT-002, NFR-011): the channel of the settings, and on ARM64 the feed of its own
/// that <c>cl package --runtime win-arm64</c> writes, so an x64 copy never downloads an ARM64 package nor the reverse.
/// </summary>
public sealed class UpdateChannelsTests
{
    [Theory]
    [Trait("Req", "ACT-002")]
    [Trait("Req", "NFR-011")]
    [InlineData(UpdateChannel.Stable, Architecture.X64, "stable")]
    [InlineData(UpdateChannel.Beta, Architecture.X64, "beta")]
    [InlineData(UpdateChannel.Stable, Architecture.Arm64, "stable-arm64")]
    [InlineData(UpdateChannel.Beta, Architecture.Arm64, "beta-arm64")]
    public void Each_architecture_reads_its_own_feed(
        UpdateChannel channel,
        Architecture architecture,
        string expected
    ) => UpdateChannels.NameOf(channel, architecture).ShouldBe(expected);
}
