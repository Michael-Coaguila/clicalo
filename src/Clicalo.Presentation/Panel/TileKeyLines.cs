using Clicalo.Domain.CommonActions;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.Panel;

/// <summary>
/// The key line of a tile (CUA-007, CUA-008, EJE-018): the combination a tap really sends to the app in front, with
/// the same function the engine uses (<see cref="CommonActionTable.ChordToSend"/>), written with the key labels of
/// <c>data/catalogs/keys.json</c>; abbreviated in size S, with full names for screen readers, and hidden in the Compact
/// view and with «Mostrar teclas» off (docs/04).
/// </summary>
/// <param name="CommonActions">The adaptive common actions (decision D4).</param>
/// <param name="Labels">The key labels.</param>
/// <param name="ActiveApp">The app in front, or <see langword="null"/> when none is known yet.</param>
/// <param name="AppsLanguage">The programs language (<c>keyboard.appsLang</c>).</param>
/// <param name="Language">The interface language.</param>
/// <param name="Shown">Whether keys show: «Mostrar teclas» on and not the Compact view.</param>
/// <param name="Abbreviated">Size S (CUA-008).</param>
public sealed record TileKeyLines(
    CommonActionTable CommonActions,
    KeyLabelCatalog Labels,
    ProcessName? ActiveApp,
    LangCode AppsLanguage,
    LangCode Language,
    bool Shown,
    bool Abbreviated
)
{
    /// <summary>The key line of <paramref name="shortcut"/>; none for an action without a combination.</summary>
    /// <param name="shortcut">The shortcut.</param>
    public TileKeyLine For(Shortcut shortcut)
    {
        ArgumentNullException.ThrowIfNull(shortcut);
        if (!Shown)
        {
            return TileKeyLine.None;
        }

        var chord = shortcut.Action switch
        {
            TapAction tap => CommonActions.ChordToSend(shortcut, tap, ActiveApp, AppsLanguage),
            HoldAction hold => hold.Chord,
            ToggleAction toggle => toggle.Chord,
            _ => null,
        };
        if (chord is null || chord.IsEmpty)
        {
            return TileKeyLine.None;
        }

        return new TileKeyLine(
            KeyChordFormatter.Format(
                chord,
                Labels,
                Abbreviated ? KeyLabelStyle.Abbreviated : KeyLabelStyle.Full,
                Language,
                LangCode.Es
            ),
            KeyChordFormatter.Format(chord, Labels, KeyLabelStyle.Spoken, Language, LangCode.Es)
        );
    }
}
