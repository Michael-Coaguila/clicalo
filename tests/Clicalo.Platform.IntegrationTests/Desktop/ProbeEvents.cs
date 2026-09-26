using Clicalo.TestKit.Windows.Input;
using Clicalo.TestKit.Windows.Probe;

namespace Clicalo.Platform.IntegrationTests.Desktop;

/// <summary>Filters and checks over the events recorded by the probe during one test.</summary>
internal static class ProbeEvents
{
    /// <summary>Key messages produced by the test injector (their <c>dwExtraInfo</c> is its marker).</summary>
    public static List<KeyMessageEvent> InjectedKeys(IEnumerable<ProbeEvent> events) =>
        [
            .. events
                .OfType<KeyMessageEvent>()
                .Where(key => key.ExtraInfo == TestKeyboardInjector.ExtraInfoMarker),
        ];

    /// <summary>Raw Input records produced by the test injector.</summary>
    public static List<RawKeyboardEvent> InjectedRaw(IEnumerable<ProbeEvent> events) =>
        [
            .. events
                .OfType<RawKeyboardEvent>()
                .Where(raw => raw.ExtraInfo == TestKeyboardInjector.ExtraInfoMarker),
        ];

    /// <summary>The <c>WM_CHAR</c> messages (text typed into the window).</summary>
    public static List<CharMessageEvent> TypedChars(IEnumerable<ProbeEvent> events) =>
        [.. events.OfType<CharMessageEvent>().Where(message => message.IsTyped)];

    /// <summary>
    /// Fails when key messages that the injector did not produce arrived (someone typed on the physical
    /// keyboard during the test), because they would make the result meaningless.
    /// </summary>
    public static void ShouldHaveNoForeignKeys(
        IEnumerable<ProbeEvent> events,
        Func<KeyMessageEvent, bool>? allowed = null
    )
    {
        var foreign = events
            .OfType<KeyMessageEvent>()
            .Where(key =>
                key.ExtraInfo != TestKeyboardInjector.ExtraInfoMarker
                && !(allowed?.Invoke(key) ?? false)
            )
            .ToList();
        foreign.ShouldBeEmpty(
            "Key messages not sent by the test arrived during it (physical keyboard input?)."
        );
    }
}
