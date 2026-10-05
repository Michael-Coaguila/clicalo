using Clicalo.Domain.Keys;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.CommonActions;

/// <summary>
/// A common action of the library and the seed (decision D4, <c>data/catalogs/common-actions.json</c>): its standard
/// combination and the exceptions of the app families whose programs language moves it.
/// </summary>
/// <param name="Id">The item id in the seed and the library (<c>save</c>, <c>selall</c>…).</param>
/// <param name="Standard">The combination every app outside the exceptions gets.</param>
/// <param name="Overrides">The exceptions, in table order; the first that matches wins.</param>
public sealed record CommonAction(
    string Id,
    KeyChord Standard,
    ValueList<CommonActionOverride> Overrides
)
{
    /// <summary>
    /// The combination for <paramref name="app"/> with the programs in <paramref name="appsLanguage"/>: the first
    /// exception whose family holds the app and whose language matches, or <see cref="Standard"/>. Without an app or a
    /// language it is <see cref="Standard"/>.
    /// </summary>
    /// <param name="app">The app in front, or <see langword="null"/> before the first one is known.</param>
    /// <param name="appsLanguage">The programs language of the keyboard settings.</param>
    public KeyChord ChordFor(ProcessName? app, LangCode? appsLanguage)
    {
        if (app is not { IsEmpty: false } process || appsLanguage is not { } language)
        {
            return Standard;
        }

        foreach (var exception in Overrides)
        {
            if (exception.AppsLanguage == language && exception.Processes.Contains(process))
            {
                return exception.Chord;
            }
        }

        return Standard;
    }

    /// <summary>
    /// Whether <paramref name="chord"/> is one of the combinations of the table (the standard one or that of an
    /// exception), compared by canonical key (REP-001). A shortcut whose combination the user changed to anything else
    /// keeps its own combination (decision D4).
    /// </summary>
    /// <param name="chord">The saved combination of a shortcut.</param>
    public bool IsTableChord(KeyChord chord)
    {
        ArgumentNullException.ThrowIfNull(chord);
        if (!CanonicalChord.TryFrom(chord, out var canonical))
        {
            return false;
        }

        if (Same(Standard, canonical))
        {
            return true;
        }

        foreach (var exception in Overrides)
        {
            if (Same(exception.Chord, canonical))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Same(KeyChord chord, CanonicalChord canonical) =>
        CanonicalChord.TryFrom(chord, out var other) && other == canonical;
}
