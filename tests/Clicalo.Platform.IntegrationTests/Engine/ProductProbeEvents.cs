using Clicalo.Platform.Core.Injection;
using Clicalo.TestKit.Windows.Probe;

namespace Clicalo.Platform.IntegrationTests.Engine;

/// <summary>What the probe received from the product's injector: every event it sends carries <c>dwExtraInfo</c> "CLKP".</summary>
internal static class ProductProbeEvents
{
    /// <summary>Key messages the product injected.</summary>
    public static List<KeyMessageEvent> Keys(IEnumerable<ProbeEvent> events) =>
        [
            .. events
                .OfType<KeyMessageEvent>()
                .Where(static key => key.ExtraInfo == LowLevelInjector.ExtraInfo),
        ];

    /// <summary>Raw Input records of the product's keys.</summary>
    public static List<RawKeyboardEvent> Raw(IEnumerable<ProbeEvent> events) =>
        [
            .. events
                .OfType<RawKeyboardEvent>()
                .Where(static raw => raw.ExtraInfo == LowLevelInjector.ExtraInfo),
        ];

    /// <summary>Mouse buttons the product injected.</summary>
    public static List<MouseButtonEvent> Buttons(IEnumerable<ProbeEvent> events) =>
        [
            .. events
                .OfType<MouseButtonEvent>()
                .Where(static button => button.ExtraInfo == LowLevelInjector.ExtraInfo),
        ];

    /// <summary>Fails when key messages the product did not inject arrived (physical keyboard input during the test).</summary>
    public static void ShouldHaveNoForeignKeys(IEnumerable<ProbeEvent> events) =>
        events
            .OfType<KeyMessageEvent>()
            .Where(static key => key.ExtraInfo != LowLevelInjector.ExtraInfo)
            .ShouldBeEmpty(
                "Key messages not sent by the product arrived during the test (physical keyboard input?)."
            );
}
