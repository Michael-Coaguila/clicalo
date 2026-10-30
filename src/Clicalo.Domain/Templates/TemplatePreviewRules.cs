using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Templates;

/// <summary>
/// The rules of the preview of Plantillas (PLA-013, PLA-015, PLA-017, LOG-008). Pure.
/// </summary>
public static class TemplatePreviewRules
{
    /// <summary>
    /// The profile installed from the template <paramref name="templateId"/> (its catalog reference, DAT-004), or
    /// <see langword="null"/>. An installed template is never overwritten: the preview offers what is missing (PLA-013).
    /// </summary>
    /// <param name="library">The shortcuts and profiles of the document.</param>
    /// <param name="templateId">The template id.</param>
    public static Profile? InstalledFrom(ShortcutLibrary library, string templateId)
    {
        ArgumentNullException.ThrowIfNull(library);
        return library.Profiles.Items.FirstOrDefault(p =>
            p.Origin is { } origin
            && string.Equals(origin.Source, templateId, StringComparison.Ordinal)
        );
    }

    /// <summary>
    /// Whether <paramref name="candidate"/> is already in <paramref name="installed"/> («ya está», PLA-015): the same
    /// catalog item, or the same combination.
    /// </summary>
    /// <param name="installed">The installed profile.</param>
    /// <param name="candidate">A shortcut of the preview.</param>
    public static bool IsAlreadyIn(Profile installed, Shortcut candidate)
    {
        ArgumentNullException.ThrowIfNull(installed);
        ArgumentNullException.ThrowIfNull(candidate);
        var key = KeyOf(candidate);
        return installed.Shortcuts.Items.Any(s =>
            (
                s.Origin is { } origin
                && candidate.Origin is { } wanted
                && string.Equals(origin.Source, wanted.Source, StringComparison.Ordinal)
                && string.Equals(origin.ItemId, wanted.ItemId, StringComparison.Ordinal)
            ) || (key is not null && string.Equals(KeyOf(s), key, StringComparison.Ordinal))
        );
    }

    /// <summary>
    /// Whether a shortcut opens, types or runs something (Web, App, Macro or Text): from a shared profile, it shows a
    /// warning and starts unchecked, so each one is confirmed before installing (PLA-015, LOG-008).
    /// </summary>
    /// <param name="shortcut">A shortcut of the preview.</param>
    public static bool IsRisky(Shortcut shortcut)
    {
        ArgumentNullException.ThrowIfNull(shortcut);
        return shortcut.Action.Kind
            is ActionKind.Url
                or ActionKind.App
                or ActionKind.Macro
                or ActionKind.Text;
    }

    /// <summary>The final button (PLA-017).</summary>
    /// <param name="installed">Whether the template is installed.</param>
    /// <param name="missing">How many of its shortcuts are not in the installed profile.</param>
    /// <param name="fromAi">Whether the preview shows an AI proposal.</param>
    public static PreviewAction ActionFor(bool installed, int missing, bool fromAi) =>
        installed ? (missing > 0 ? PreviewAction.AddMissing : PreviewAction.EditShortcuts)
        : fromAi ? PreviewAction.CreateWith
        : PreviewAction.Install;

    /// <summary>
    /// The name a row installs with (PLA-015): what the person typed, the same in every language, or the original when
    /// the edit is empty.
    /// </summary>
    /// <param name="original">The name of the template.</param>
    /// <param name="edited">The edit, or <see langword="null"/>.</param>
    public static LocalizedText NameFor(LocalizedText original, string? edited)
    {
        ArgumentNullException.ThrowIfNull(original);
        var text = (edited ?? string.Empty).Trim();
        return text.Length == 0 ? original : LocalizedText.Same(text, LangCode.Es, LangCode.En);
    }

    private static string? KeyOf(Shortcut shortcut)
    {
        var chord = shortcut.Action switch
        {
            TapAction tap => tap.Chord,
            HoldAction hold => hold.Chord,
            ToggleAction toggle => toggle.Chord,
            _ => null,
        };
        return chord is not null && CanonicalChord.TryFrom(chord, out var canonical)
            ? shortcut.Action.Kind + ":" + canonical.ToStableString()
            : null;
    }
}
