using Clicalo.Application.Localization;
using Clicalo.Application.UseCases.Editor;

namespace Clicalo.Presentation.ControlCenter.About;

/// <summary>
/// What «Acerca de y contacto» works with (ACE-001 to ACE-005). The composition root builds it: nothing here leaves
/// the computer without a tap of the person (LOG-002).
/// </summary>
/// <param name="Localization">The interface language.</param>
/// <param name="Version">The real version of the app («2.0.0»).</param>
/// <param name="System">The real version of Windows, for «Incluir versión y sistema».</param>
/// <param name="Links">The repository, the issues and, if they exist, LinkedIn and the contact email.</param>
/// <param name="Open">Opens an address in the browser or the email app; false when it could not.</param>
/// <param name="Copy">Copies a text to the clipboard.</param>
/// <param name="Dictate">Starts Windows dictation (Win+H) for the focused field (ACC-011).</param>
/// <param name="ReadLog">
/// The log exactly as it would be attached, already redacted on write (LOG-001); empty when there is nothing yet and
/// <see langword="null"/> when it could not be read.
/// </param>
/// <param name="SaveLog">
/// Saves that text as a file next to the log and opens its folder with the file selected (ACE-004); its file name, or
/// <see langword="null"/> when it could not.
/// </param>
/// <param name="Notify">Shows a message in the status bar of the Control Center.</param>
public sealed record AboutServices(
    ILocalizationContext Localization,
    string Version,
    string System,
    AboutLinks Links,
    Func<Uri, CancellationToken, ValueTask<bool>> Open,
    Action<string> Copy,
    Func<CancellationToken, ValueTask<bool>> Dictate,
    Func<CancellationToken, ValueTask<string?>> ReadLog,
    Func<string, CancellationToken, ValueTask<string?>> SaveLog,
    Action<WorkspaceNotice> Notify
);
