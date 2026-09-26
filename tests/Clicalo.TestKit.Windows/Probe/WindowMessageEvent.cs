using System.Text.Json;

namespace Clicalo.TestKit.Windows.Probe;

/// <summary>
/// Any other recorded message: IME (<c>WM_IME_*</c>), <c>WM_SYSCOMMAND</c>, <c>WM_NCACTIVATE</c> and
/// <c>WM_MOUSEACTIVATE</c>. Only the raw parameters are available.
/// </summary>
public sealed class WindowMessageEvent : ProbeMessageEvent
{
    internal WindowMessageEvent(JsonElement json, string line)
        : base(json, line) { }
}
