using System.Collections.Immutable;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Privacy;

namespace Clicalo.Application.UseCases.Editor;

/// <summary>
/// The «Qué hace» grid of the editor (EDI-006): the eight kinds in their order and what a change of kind keeps. Pure.
/// </summary>
/// <remarks>
/// Press, Hold and Toggle keep the combination between them. Mouse starts with the last mouse action of the shortcut
/// or with a right click. Any other kind starts empty and stays «Incompleto» until filled: no real example values
/// (decision DIS-54). Leaving Mouse drops the mouse action, since it is a different kind. The editor passes what the
/// same shortcut had in the new kind while it is open, so switching back and forth loses nothing.
/// </remarks>
public static class ActionKinds
{
    /// <summary>The kinds of the grid, in its order (4 × 2): Pulsar, Mantener, Alternar, Texto, Mouse, Macro, Web, App.</summary>
    public static ImmutableArray<ActionKind> Grid { get; } =
    [
        ActionKind.Tap,
        ActionKind.Hold,
        ActionKind.Toggle,
        ActionKind.Text,
        ActionKind.Mouse,
        ActionKind.Macro,
        ActionKind.Url,
        ActionKind.App,
    ];

    /// <summary>Whether the kind is edited with the combination box (EDI-007).</summary>
    /// <param name="kind">The kind.</param>
    public static bool HasKeys(ActionKind kind) =>
        kind is ActionKind.Tap or ActionKind.Hold or ActionKind.Toggle;

    /// <summary>The combination of a Press, Hold or Toggle action; null for the others.</summary>
    /// <param name="action">The action.</param>
    public static KeyChord? ChordOf(ShortcutAction action) =>
        action switch
        {
            TapAction tap => tap.Chord,
            HoldAction hold => hold.Chord,
            ToggleAction toggle => toggle.Chord,
            _ => null,
        };

    /// <summary>The same action with another combination (only Press, Hold and Toggle have one).</summary>
    /// <param name="action">The action.</param>
    /// <param name="chord">The new combination.</param>
    public static ShortcutAction WithChord(ShortcutAction action, KeyChord chord) =>
        action switch
        {
            TapAction tap => tap with { Chord = chord, Variants = [] },
            HoldAction hold => hold with { Chord = chord },
            ToggleAction toggle => toggle with { Chord = chord },
            _ => action,
        };

    /// <summary>The action after choosing <paramref name="kind"/> in the grid.</summary>
    /// <param name="current">The current action.</param>
    /// <param name="kind">The chosen kind.</param>
    /// <param name="remembered">What the shortcut had in that kind earlier in this edit, if anything.</param>
    public static ShortcutAction Switch(
        ShortcutAction current,
        ActionKind kind,
        ShortcutAction? remembered
    )
    {
        ArgumentNullException.ThrowIfNull(current);
        if (current.Kind == kind)
        {
            return current;
        }

        var chord = ChordOf(current) ?? (remembered is null ? null : ChordOf(remembered));
        return kind switch
        {
            ActionKind.Tap => new TapAction(chord ?? KeyChord.Empty, []),
            ActionKind.Hold => new HoldAction(chord ?? KeyChord.Empty),
            ActionKind.Toggle => new ToggleAction(chord ?? KeyChord.Empty),
            ActionKind.Mouse => remembered as MouseAction
                ?? new MouseAction(MouseOp.RightClick, ScrollSpeed.Normal),
            ActionKind.Text => remembered as TextAction
                ?? new TextAction(SecretText.Empty, TextMethod.Unicode),
            ActionKind.Macro => remembered as MacroAction ?? new MacroAction([]),
            ActionKind.Url => remembered as UrlAction
                ?? new UrlAction(new UrlTarget.Raw(string.Empty)),
            ActionKind.App => remembered as AppAction
                ?? new AppAction(new AppTarget.Raw(string.Empty)),
            _ => current,
        };
    }
}
