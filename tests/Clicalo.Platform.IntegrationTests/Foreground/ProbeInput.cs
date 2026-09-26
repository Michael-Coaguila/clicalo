using Clicalo.Platform.IntegrationTests.Desktop;
using Clicalo.TestKit.Windows.Input;
using Clicalo.TestKit.Windows.Probe;

namespace Clicalo.Platform.IntegrationTests.Foreground;

/// <summary>What the foreground tests look for in the InputProbe (spike S4 criteria).</summary>
internal static class ProbeInput
{
    private const uint SystemCommand = 0x0112;
    private const ulong KeyMenu = 0xF100;

    /// <summary>
    /// Presses of the reserved key (<c>WM_KEYDOWN</c>/<c>WM_SYSKEYDOWN</c> of F24) that reached the probe: must be
    /// none, because the system consumes the registered chord.
    /// </summary>
    public static List<KeyMessageEvent> ReservedKeyPresses(IEnumerable<ProbeEvent> events) =>
        [.. ReservedKeyMessages(events).Where(key => key.IsPress)];

    /// <summary>
    /// Releases of F24 that reached the probe. <c>RegisterHotKey</c> consumes the press only: the release arrives
    /// alone (spike S4 records it; it produces no character and no command).
    /// </summary>
    public static List<KeyMessageEvent> ReservedKeyReleases(IEnumerable<ProbeEvent> events) =>
        [.. ReservedKeyMessages(events).Where(key => key.IsRelease)];

    /// <summary><c>WM_SYSCOMMAND(SC_KEYMENU)</c> in the probe: a menu opened by Alt. Must be none.</summary>
    public static List<WindowMessageEvent> KeyMenus(IEnumerable<ProbeEvent> events) =>
        [
            .. events
                .OfType<WindowMessageEvent>()
                .Where(message =>
                    message.Message == SystemCommand && (message.WParam & 0xFFF0) == KeyMenu
                ),
        ];

    /// <summary>The modifier messages of the injected chords that reached the probe (recorded, not failed on).</summary>
    public static List<KeyMessageEvent> ChordModifiers(IEnumerable<ProbeEvent> events) =>
        [
            .. ProbeEvents
                .InjectedKeys(events)
                .Where(key =>
                    key.VirtualKey
                        is VirtualKeyCode.Control
                            or VirtualKeyCode.Menu
                            or VirtualKeyCode.Shift
                ),
        ];

    /// <summary>One line for the test output: how many messages of each kind and key.</summary>
    public static string Summary(IEnumerable<KeyMessageEvent> keys) =>
        string.Join(
            ", ",
            keys.GroupBy(key => key.MessageName + " " + key.SideVirtualKey, StringComparer.Ordinal)
                .Select(group => group.Key + " × " + group.Count())
        );

    private static IEnumerable<KeyMessageEvent> ReservedKeyMessages(
        IEnumerable<ProbeEvent> events
    ) =>
        events
            .OfType<KeyMessageEvent>()
            .Where(key => key.VirtualKey == GuardedInternalKeyEffects.F24);
}
