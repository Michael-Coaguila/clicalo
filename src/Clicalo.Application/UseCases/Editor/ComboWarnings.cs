using System.Collections.Frozen;
using Clicalo.Domain.Keys;

namespace Clicalo.Application.UseCases.Editor;

/// <summary>
/// The blocked and special combinations of <c>data/catalogs/blocked-combos.json</c> (docs/03 §7, EJE-014), as the
/// editor warns about them (EDI-007). Compared as canonical keys where «Ctrl» without a side equals any Ctrl
/// (REP-001), so no side slips a blocked combination through. <c>ComboWarningsTests</c> compares the lists with the
/// JSON. Pure.
/// </summary>
public static class ComboWarnings
{
    /// <summary>The blocked combinations, as key ids in the order of the JSON.</summary>
    public static IReadOnlyList<IReadOnlyList<string>> BlockedKeys { get; } =
    [
        ["ctrl", "alt", "delete"],
        ["win", "l"],
    ];

    /// <summary>The special combinations, as key ids in the order of the JSON.</summary>
    public static IReadOnlyList<IReadOnlyList<string>> SpecialKeys { get; } =
    [
        ["win", "g"],
        ["alt", "tab"],
        ["win", "tab"],
        ["ctrl", "shift", "esc"],
    ];

    private static FrozenSet<string> Blocked { get; } = Keys(BlockedKeys);

    private static FrozenSet<string> Special { get; } = Keys(SpecialKeys);

    /// <summary>The warning of <paramref name="chord"/>.</summary>
    /// <param name="chord">The combination of the box.</param>
    public static ComboWarning Of(KeyChord? chord)
    {
        if (chord is null || KeyOf(chord) is not { } key)
        {
            return ComboWarning.None;
        }

        return Blocked.Contains(key) ? ComboWarning.Blocked
            : Special.Contains(key) ? ComboWarning.Special
            : ComboWarning.None;
    }

    private static string? KeyOf(KeyChord chord) =>
        CanonicalChord.TryFrom(chord, out var canonical)
            ? canonical.ForBlockedComparison().ToStableString()
            : null;

    private static FrozenSet<string> Keys(IReadOnlyList<IReadOnlyList<string>> combos) =>
        combos
            .Select(static ids =>
                KeyOf(KeyChord.Create(ids.Select(static id => new KeyStroke(new KeyId(id)))))
            )
            .OfType<string>()
            .ToFrozenSet(StringComparer.Ordinal);
}
