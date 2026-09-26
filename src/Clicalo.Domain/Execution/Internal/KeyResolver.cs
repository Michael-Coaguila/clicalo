using System.Collections.Immutable;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Execution.Internal;

/// <summary>
/// Resolves a combination to the physical keys to press, in press order (EJE-003), against the foreground layout
/// (blueprint §7.7): a character that needs Shift or AltGr in that layout gets it right before it. The same physical
/// key is pressed once.
/// </summary>
internal static class KeyResolver
{
    /// <summary>Resolves <paramref name="strokes"/>; <see langword="false"/> when a key does not exist in the layout.</summary>
    /// <param name="strokes">The combination, in press order.</param>
    /// <param name="mode">The injection mode of the plan.</param>
    /// <param name="layout">The foreground layout.</param>
    /// <param name="keys">The physical keys in press order.</param>
    public static bool TryResolve(
        ValueList<KeyStroke> strokes,
        InjectionMode mode,
        KeyboardLayoutSnapshot layout,
        out ImmutableArray<InjectedKey> keys
    )
    {
        var resolved = ImmutableArray.CreateBuilder<InjectedKey>(strokes.Count + 2);
        foreach (var stroke in strokes)
        {
            if (stroke.Key.IsCharacter)
            {
                if (!layout.TryGetCharacter(stroke.Key, out var character))
                {
                    keys = [];
                    return false;
                }

                if (
                    character.NeedsAltGr
                    && !TryAdd(resolved, new KeyStroke(KeyIds.Alt, KeySide.Right), mode, layout)
                )
                {
                    keys = [];
                    return false;
                }

                if (
                    character.NeedsShift
                    && !TryAdd(resolved, new KeyStroke(KeyIds.Shift), mode, layout)
                )
                {
                    keys = [];
                    return false;
                }
            }

            if (!TryAdd(resolved, stroke, mode, layout))
            {
                keys = [];
                return false;
            }
        }

        keys = resolved.ToImmutable();
        return true;
    }

    private static bool TryAdd(
        ImmutableArray<InjectedKey>.Builder resolved,
        KeyStroke stroke,
        InjectionMode mode,
        KeyboardLayoutSnapshot layout
    )
    {
        if (!layout.TryResolve(stroke, mode, out var key))
        {
            return false;
        }

        if (!resolved.Contains(key))
        {
            resolved.Add(key);
        }

        return true;
    }
}
