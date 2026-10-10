using System.Globalization;
using System.Windows.Input;
using Clicalo.Domain.Keys;

namespace Clicalo.UI.Wpf.Workspace.Internal;

/// <summary>
/// The catalog key of a key the Control Center window receives while «Grabar con teclado» is on (EDI-010): letters,
/// digits, function keys, the numeric pad, navigation and media keys, and the modifiers with their side. Punctuation
/// keys depend on the layout and are not recorded: like Esc and the combinations Windows intercepts, they are chosen
/// in the key picker.
/// </summary>
internal static class RecordedKeys
{
    /// <summary>The catalog key of <paramref name="e"/>, or null when the catalog has none for it.</summary>
    /// <param name="e">The key event; with Alt held, WPF reports the key as the system key.</param>
    public static KeyId? IdOf(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        return IdOf(e.Key == Key.System ? e.SystemKey : e.Key);
    }

    /// <summary>The catalog key of <paramref name="key"/>, or null when the catalog has none for it.</summary>
    /// <param name="key">The WPF key.</param>
    public static KeyId? IdOf(Key key) => Name(key) is { } id ? new KeyId(id) : null;

    private static string? Name(Key key) =>
        key switch
        {
            >= Key.A and <= Key.Z => ((char)('a' + (key - Key.A))).ToString(),
            >= Key.D0 and <= Key.D9 => Number(key - Key.D0),
            >= Key.F1 and <= Key.F24 => "f" + Number(key - Key.F1 + 1),
            >= Key.NumPad0 and <= Key.NumPad9 => "num." + Number(key - Key.NumPad0),
            Key.LeftCtrl => "lctrl",
            Key.RightCtrl => "rctrl",
            Key.LeftShift => "lshift",
            Key.RightShift => "rshift",
            Key.LeftAlt => "lalt",
            Key.RightAlt => "altgr",
            Key.LWin => "win",
            Key.RWin => "rwin",
            Key.Enter => "enter",
            Key.Tab => "tab",
            Key.Escape => "esc",
            Key.Space => "space",
            Key.Delete => "delete",
            Key.Back => "backspace",
            Key.Home => "home",
            Key.End => "end",
            Key.PageUp => "pageup",
            Key.PageDown => "pagedown",
            Key.Left => "left",
            Key.Right => "right",
            Key.Up => "up",
            Key.Down => "down",
            Key.PrintScreen => "printscreen",
            Key.Apps => "menu",
            Key.Add => "num.add",
            Key.Subtract => "num.subtract",
            Key.Multiply => "num.multiply",
            Key.Divide => "num.divide",
            Key.Decimal => "num.decimal",
            Key.NumLock => "numlock",
            Key.VolumeUp => "volume.up",
            Key.VolumeDown => "volume.down",
            Key.VolumeMute => "volume.mute",
            Key.MediaPlayPause => "media.playpause",
            Key.MediaNextTrack => "media.next",
            Key.MediaPreviousTrack => "media.previous",
            Key.MediaStop => "media.stop",
            Key.LaunchApplication2 => "launch.calculator",
            Key.BrowserHome => "launch.browser",
            Key.LaunchMail => "launch.mail",
            Key.CapsLock => "capslock",
            Key.Insert => "insert",
            Key.Pause => "pause",
            Key.Scroll => "scrolllock",
            _ => null,
        };

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
