using Clicalo.Application.UseCases.Editor;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Privacy;

namespace Clicalo.Presentation.ControlCenter.Editor;

/// <summary>
/// What a Web, App or Macro shortcut does, said whole for the review before an imported one is installed (LOG-008):
/// the address, the target with its arguments, or every step of the macro in order, texts included. It is never cut:
/// the person confirms what they read, so a step or an argument hidden after «…» could not be confirmed.
/// </summary>
public static class ActionDetail
{
    private const string Then = " → ";

    /// <summary>The whole detail of <paramref name="action"/>; empty for the kinds LOG-008 does not review.</summary>
    /// <param name="action">What the shortcut does.</param>
    /// <param name="catalogs">The names of keys and mouse actions.</param>
    /// <param name="language">The interface language.</param>
    /// <param name="text">Formats a message in the interface language.</param>
    public static string Of(
        ShortcutAction action,
        EditorCatalogs catalogs,
        LangCode language,
        Func<Message, string> text
    )
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(catalogs);
        ArgumentNullException.ThrowIfNull(text);
        return action switch
        {
            UrlAction url => Targets.Text(url.Target),
            AppAction app => Targets.Text(app.Target),
            MacroAction macro => string.Join(
                Then,
                macro.Steps.Items.Select(step => Step(step, catalogs, language, text))
            ),
            _ => string.Empty,
        };
    }

    private static string Step(
        MacroStep step,
        EditorCatalogs catalogs,
        LangCode language,
        Func<Message, string> text
    ) =>
        step switch
        {
            KeysStep keys => keys.Chord.IsEmpty
                ? text(L.ComboNone)
                : text(
                    L.StepPress(
                        keys: KeyChordFormatter.Format(
                            keys.Chord,
                            catalogs.KeyLabels,
                            KeyLabelStyle.Full,
                            language,
                            LangCode.Es
                        )
                    )
                ),
            WaitStep wait => text(
                L.StepWait(seconds: (decimal)wait.Duration.TotalMilliseconds / 1000m)
            ),
            TextStep typed => text(L.StepType(name: Reveal(typed.Text))),
            MouseStep mouse => catalogs
                .MouseActions.Items.FirstOrDefault(m => m.Op == mouse.Op)
                ?.Label.Get(language, LangCode.Es)
                ?? string.Empty,
            _ => string.Empty,
        };

    private static string Reveal(SecretText secret)
    {
        var result = string.Empty;
        secret.WithRevealed(0, (span, _) => result = new string(span));
        return result;
    }
}
