using System.Collections.Frozen;
using System.Collections.Immutable;
using Clicalo.Domain.Execution.Internal;
using Clicalo.Domain.Keys;

namespace Clicalo.Domain.Execution;

/// <summary>
/// A pure table of the foreground thread's layout, captured on every foreground change and on
/// <c>WM_INPUTLANGCHANGE</c> (blueprint §7.7). Fixed keys take <c>vk</c>, <c>scan</c> and <c>extended</c> from
/// <c>keys.win32.json</c>; characters come from this table. A key missing from the layout sends nothing and warns
/// (EC-EJE-10).
/// </summary>
/// <param name="Layout">The layout.</param>
/// <param name="Characters">How each character key of the catalog is typed in this layout.</param>
public sealed record KeyboardLayoutSnapshot(
    KeyboardLayoutId Layout,
    ImmutableDictionary<KeyId, LayoutKey> Characters
)
{
    // (base key, side) → the sided key of the catalog: ctrl + Right → rctrl. A generic modifier is sent as its left
    // key (docs/03 §3), which is the base key's own entry.
    private static readonly FrozenDictionary<(string Key, KeySide Side), string> SidedKeys =
        KeyDefinitions
            .All.Where(static d => d.BaseKey is not null && d.Side != KeySide.Any)
            .ToFrozenDictionary(
                static d => (d.BaseKey!.Value.Value, d.Side),
                static d => d.Id.Value
            );

    // sided key → base key, so a stroke that names a sided key directly (lctrl) resolves like ctrl + Left.
    private static readonly FrozenDictionary<string, KeyDefinition> Definitions =
        KeyDefinitions.All.ToFrozenDictionary(static d => d.Id.Value, StringComparer.Ordinal);

    /// <summary>No layout known yet: fixed keys resolve, characters do not.</summary>
    public static KeyboardLayoutSnapshot Empty { get; } =
        new(default, ImmutableDictionary<KeyId, LayoutKey>.Empty);

    /// <summary>Resolves a stroke to the physical key for <paramref name="mode"/> (table of blueprint §7.7).</summary>
    /// <param name="stroke">The stroke; a side selects the left or right key.</param>
    /// <param name="mode">The mode of the plan.</param>
    /// <param name="key">The physical key.</param>
    public bool TryResolve(KeyStroke stroke, InjectionMode mode, out InjectedKey key)
    {
        if (stroke.Key.IsCharacter)
        {
            if (Characters.TryGetValue(stroke.Key, out var character))
            {
                key =
                    mode == InjectionMode.ScanCode
                        ? new InjectedKey(0, character.Scan, character.Extended, mode)
                        : new InjectedKey(character.Vk, character.Scan, character.Extended, mode);
                return character.Vk != 0 || character.Scan != 0;
            }

            key = default;
            return false;
        }

        if (!Win32FixedKeys.ById.TryGetValue(SidedId(stroke), out var fixedKey))
        {
            key = default;
            return false;
        }

        key = fixedKey switch
        {
            // Pause (E1 prefix) cannot be expressed with KEYEVENTF_EXTENDEDKEY: always by virtual key, no scan code.
            { PrefixE1: true } => new InjectedKey(fixedKey.Vk, 0, false, InjectionMode.VirtualKey),
            _ when mode == InjectionMode.ScanCode => new InjectedKey(
                0,
                fixedKey.Scan,
                fixedKey.Extended,
                mode
            ),
            _ => new InjectedKey(fixedKey.Vk, fixedKey.Scan, fixedKey.Extended, mode),
        };
        return true;
    }

    /// <summary>
    /// How a character key is typed in this layout, including the Shift or AltGr it needs; <see langword="false"/>
    /// when the layout has no such character (EC-EJE-10).
    /// </summary>
    /// <param name="key">A character key (<c>char:ñ</c>).</param>
    /// <param name="layoutKey">The key and the modifiers it needs.</param>
    public bool TryGetCharacter(KeyId key, out LayoutKey layoutKey) =>
        Characters.TryGetValue(key, out layoutKey) && (layoutKey.Vk != 0 || layoutKey.Scan != 0);

    private static string SidedId(KeyStroke stroke)
    {
        var id = stroke.Key.Value ?? string.Empty;
        if (stroke.Side == KeySide.Any)
        {
            return id;
        }

        // A sided catalog key named directly (altgr) with a side keeps its own entry.
        if (Definitions.TryGetValue(id, out var definition) && definition.BaseKey is not null)
        {
            return id;
        }

        return SidedKeys.TryGetValue((id, stroke.Side), out var sided) ? sided : id;
    }
}
