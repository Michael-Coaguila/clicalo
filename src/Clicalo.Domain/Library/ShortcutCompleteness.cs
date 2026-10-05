using Clicalo.Domain.Keys;

namespace Clicalo.Domain.Library;

/// <summary>
/// The completeness rule of ATJ-009, the only one: the editor marks with it, the panel refuses with it (EJE-015) and
/// the facet test requires a rule for every <see cref="ActionKind"/>. An incomplete shortcut is still valid: it can be
/// saved and undone (blueprint §6.2).
/// </summary>
public static class ShortcutCompleteness
{
    /// <summary>What is missing in <paramref name="action"/>, or <see cref="CompletenessIssue.None"/>.</summary>
    /// <remarks>
    /// Tap, Hold and Toggle need keys; Text needs an available, non-empty text (COP-005); a macro needs steps and no
    /// empty keys step (an undecryptable text step makes it incomplete too); Web needs an absolute http or https
    /// address; App needs a target that can be started as written. Mouse and System actions are always complete.
    /// </remarks>
    /// <param name="action">The action to evaluate.</param>
    /// <exception cref="ArgumentOutOfRangeException">An action type outside the closed hierarchy (a defect).</exception>
    public static CompletenessIssue Evaluate(ShortcutAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return action switch
        {
            TapAction tap => KeysIssue(tap.Chord),
            HoldAction hold => KeysIssue(hold.Chord),
            ToggleAction toggle => KeysIssue(toggle.Chord),
            TextAction text => !text.Text.IsAvailable ? CompletenessIssue.TextUnavailable
            : text.Text.Length == 0 ? CompletenessIssue.MissingText
            : CompletenessIssue.None,
            MouseAction => CompletenessIssue.None,
            MacroAction macro => MacroIssue(macro),
            UrlAction url => IsWebAddress(url.Target)
                ? CompletenessIssue.None
                : CompletenessIssue.InvalidAddress,
            AppAction app => IsStartable(app.Target)
                ? CompletenessIssue.None
                : CompletenessIssue.InvalidApp,
            SystemAction => CompletenessIssue.None,
            _ => throw new ArgumentOutOfRangeException(
                nameof(action),
                action.Kind,
                "Unknown action type."
            ),
        };
    }

    /// <summary>
    /// What is missing in <paramref name="shortcut"/>: a name in some language first (ATJ-009), then its action.
    /// </summary>
    /// <param name="shortcut">The shortcut to evaluate.</param>
    public static CompletenessIssue Evaluate(Shortcut shortcut)
    {
        ArgumentNullException.ThrowIfNull(shortcut);
        return LibraryRules.HasName(shortcut.Name)
            ? Evaluate(shortcut.Action)
            : CompletenessIssue.MissingName;
    }

    /// <summary>
    /// Whether <paramref name="shortcut"/> is a blank draft (ATJ-011): no name in any language, a Tap without keys
    /// and without variants. A blank draft is discarded silently and never leaves a trace in the undo history.
    /// </summary>
    /// <param name="shortcut">The shortcut.</param>
    public static bool IsBlankDraft(Shortcut shortcut)
    {
        ArgumentNullException.ThrowIfNull(shortcut);
        return !LibraryRules.HasName(shortcut.Name)
            && shortcut.Action is TapAction { Chord.IsEmpty: true, Variants.IsEmpty: true };
    }

    private static CompletenessIssue KeysIssue(KeyChord chord) =>
        chord is null || chord.IsEmpty ? CompletenessIssue.MissingKeys : CompletenessIssue.None;

    private static CompletenessIssue MacroIssue(MacroAction macro)
    {
        if (macro.Steps.IsEmpty)
        {
            return CompletenessIssue.MissingSteps;
        }

        foreach (var step in macro.Steps)
        {
            switch (step)
            {
                case KeysStep keys when keys.Chord is null || keys.Chord.IsEmpty:
                    return CompletenessIssue.MissingKeys;
                case TextStep text when text.Text is null || !text.Text.IsAvailable:
                    return CompletenessIssue.TextUnavailable;
            }
        }

        return CompletenessIssue.None;
    }

    private static bool IsWebAddress(UrlTarget target) =>
        target is UrlTarget.Valid { Address: { IsAbsoluteUri: true } address }
        && (
            string.Equals(address.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || string.Equals(address.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
        )
        && !string.IsNullOrEmpty(address.Host);

    private static bool IsStartable(AppTarget target) =>
        target switch
        {
            AppTarget.Executable executable => !string.IsNullOrWhiteSpace(executable.Path),
            AppTarget.StoreApp store => !string.IsNullOrWhiteSpace(store.AppUserModelId),
            AppTarget.Document document => !string.IsNullOrWhiteSpace(document.Path),
            _ => false,
        };
}
