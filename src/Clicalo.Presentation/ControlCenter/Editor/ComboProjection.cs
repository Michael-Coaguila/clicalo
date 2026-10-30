using System.Collections.Immutable;
using Clicalo.Application.UseCases.Editor;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.ControlCenter.Editor;

/// <summary>
/// Projects a combination into the model of the combination box and its key picker (EDI-007 to EDI-009), for the
/// editor of a shortcut and for a row of the preview of Plantillas (PLA-016). Pure.
/// </summary>
internal static class ComboProjection
{
    private static readonly ImmutableArray<KeyId> ModifierKeys =
    [
        KeyIds.Ctrl,
        KeyIds.Alt,
        KeyIds.Shift,
        KeyIds.Win,
    ];

    private static readonly ImmutableArray<KeyGroup> PickerGroups =
    [
        KeyGroup.Sides,
        KeyGroup.Letters,
        KeyGroup.Nums,
        KeyGroup.Fn,
        KeyGroup.Special,
        KeyGroup.Numpad,
        KeyGroup.Media,
    ];

    /// <summary>The model of <paramref name="chord"/> with the picker on <paramref name="group"/>.</summary>
    /// <param name="chord">The combination of the box.</param>
    /// <param name="group">The group the picker shows.</param>
    /// <param name="labels">The names of the keys.</param>
    /// <param name="language">The interface language.</param>
    /// <param name="format">Formats a message in the interface language.</param>
    /// <param name="isTap">
    /// Whether the combination is the one of a Tap: only then the engine runs the system alternative of a blocked
    /// combination (EJE-014), and the warning says so (EDI-007).
    /// </param>
    public static ComboModel Build(
        KeyChord chord,
        KeyGroup group,
        KeyLabelCatalog labels,
        LangCode language,
        Func<Message, string> format,
        bool isTap
    )
    {
        var warning = ComboWarnings.Of(chord);
        return new ComboModel(
            format(L.Keys),
            null,
            format(L.KeepOld),
            chord.IsEmpty ? format(L.ComboEmpty) : null,
            [
                .. chord.Strokes.Select(
                    (stroke, i) =>
                    {
                        var label = KeyChordFormatter.KeyText(
                            stroke,
                            labels,
                            KeyLabelStyle.Full,
                            language,
                            LangCode.Es
                        );
                        return new KeyChip(i, label, format(L.RemoveKeyN(key: label)));
                    }
                ),
            ],
            chord.IsEmpty ? format(L.ComboNone) : format(L.ComboN(chord.Strokes.Count)),
            !chord.IsEmpty,
            format(L.BackKey),
            format(L.ClearKeys),
            warning switch
            {
                ComboWarning.Blocked => WarningTone.Danger,
                ComboWarning.Special => WarningTone.Warn,
                _ => WarningTone.None,
            },
            warning switch
            {
                ComboWarning.Blocked => format(BlockedText(chord, isTap)),
                ComboWarning.Special => format(L.BlockedS),
                _ => null,
            },
            null,
            format(L.Done),
            [.. ModifierKeys.Select(key => Cell(key, chord, labels, language))],
            [.. PickerGroups.Select(g => new KeyGroupTab(g, format(GroupLabel(g)), g == group))],
            [
                .. KeyDefinitions
                    .All.Where(definition => definition.Group == group)
                    .Select(definition => Cell(definition.Id, chord, labels, language)),
            ],
            group switch
            {
                KeyGroup.Letters or KeyGroup.Nums => 7,
                KeyGroup.Fn => 6,
                _ => 0,
            },
            format(L.OrderHint2),
            null,
            null,
            string.Empty
        );
    }

    // EDI-007: the warning of a blocked combination offers its alternative when it has one. Today that is Win+L,
    // which a Tap turns into the system action «Bloquear equipo».
    private static Message BlockedText(KeyChord chord, bool isTap) =>
        isTap
        && string.Equals(
            ComboWarnings.AlternativeOf(chord),
            ComboWarnings.LockCommand,
            StringComparison.Ordinal
        )
            ? L.BlockedAltLock
            : L.BlockedB;

    private static KeyCell Cell(
        KeyId key,
        KeyChord chord,
        KeyLabelCatalog labels,
        LangCode language
    )
    {
        var stroke = new KeyStroke(key);
        return new KeyCell(
            key,
            KeyChordFormatter.KeyText(stroke, labels, KeyLabelStyle.Full, language, LangCode.Es),
            KeyChordFormatter.KeyText(stroke, labels, KeyLabelStyle.Spoken, language, LangCode.Es),
            ChordEdits.IsChosen(chord, key)
        );
    }

    private static Message GroupLabel(KeyGroup group) =>
        group switch
        {
            KeyGroup.Sides => L.KgSides,
            KeyGroup.Letters => L.KgLetters,
            KeyGroup.Nums => L.KgNums,
            KeyGroup.Fn => L.KgFn,
            KeyGroup.Special => L.KgSpecial,
            KeyGroup.Numpad => L.KgNumpad,
            KeyGroup.Media => L.KgMedia,
            _ => L.KgMods,
        };
}
