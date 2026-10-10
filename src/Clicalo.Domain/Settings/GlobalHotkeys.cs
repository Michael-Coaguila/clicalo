using System.Collections.Immutable;
using Clicalo.Domain.Keys;

namespace Clicalo.Domain.Settings;

/// <summary>
/// The closed list of combinations for the global shortcut that shows or hides the panel (BUR-005, user decision D10):
/// the entries of <c>data/catalogs/global-hotkeys.json</c>, in its order, where the selection criterion is documented.
/// <c>GlobalHotkeysTests</c> compares this list with the JSON. The first one is the default choice; the shortcut itself
/// is off by default (<see cref="GlobalHotkeySettings.Enabled"/>).
/// </summary>
public static class GlobalHotkeys
{
    /// <summary>Every combination offered, in the order of the catalog.</summary>
    public static ImmutableArray<GlobalHotkey> All { get; } =
    [
        Hotkey("ctrl-alt-space", "ctrl", "alt", "space"),
        Hotkey("ctrl-alt-shift-space", "ctrl", "alt", "shift", "space"),
        Hotkey("ctrl-alt-f8", "ctrl", "alt", "f8"),
        Hotkey("ctrl-alt-f10", "ctrl", "alt", "f10"),
        Hotkey("ctrl-alt-f11", "ctrl", "alt", "f11"),
    ];

    /// <summary>The combination preselected until the person picks another: the first of the list.</summary>
    public static GlobalHotkey Default => All[0];

    /// <summary>The combination with <paramref name="id"/>, or <see langword="null"/> when it is not in the list.</summary>
    /// <param name="id">A persisted id.</param>
    public static GlobalHotkey? Find(string? id)
    {
        foreach (var hotkey in All)
        {
            if (string.Equals(hotkey.Id, id, StringComparison.Ordinal))
            {
                return hotkey;
            }
        }

        return null;
    }

    private static GlobalHotkey Hotkey(string id, params string[] keys) =>
        new(id, KeyChord.Create(keys.Select(static key => new KeyStroke(new KeyId(key)))));
}
