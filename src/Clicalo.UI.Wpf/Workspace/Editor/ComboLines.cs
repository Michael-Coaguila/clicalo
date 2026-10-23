using System.Windows.Automation;
using System.Windows.Controls;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Workspace.Editor;

/// <summary>
/// The two lines of a combination box that speak when they appear (ACC-001): the warning of a blocked or special
/// combination and the line that says the window listens to the keyboard (EDI-010). <see cref="ComboView"/> draws the
/// box again on every change, so each line is one text that lives across those draws and goes into the new box: a live
/// region that is read out once each time its text appears or changes. One per box that can be on show (the editor of a
/// shortcut, whose macro steps take turns with it, or the row of the preview of Plantillas).
/// </summary>
internal sealed class ComboLines
{
    private readonly StyledText _warning = Parts.Note(string.Empty, ColorToken.Text);
    private readonly StyledText _recording = Parts.Note(
        string.Empty,
        ColorToken.AccentInk,
        TextStyleToken.Body
    );
    private string? _warned;
    private string? _listened;

    /// <summary>Creates the lines, with nothing to say yet.</summary>
    public ComboLines()
    {
        WarningAnnouncer = new LiveAnnouncer(_warning);
        RecordingAnnouncer = new LiveAnnouncer(_recording);
    }

    /// <summary>What the warning said last, and how.</summary>
    public LiveAnnouncer WarningAnnouncer { get; }

    /// <summary>What the line of the recording said last, and how.</summary>
    public LiveAnnouncer RecordingAnnouncer { get; }

    /// <summary>
    /// The text of the warning <paramref name="text"/>, taken out of the box it was in: assertive for a blocked
    /// combination, polite for a special one, and read out only when it was not on show already.
    /// </summary>
    /// <param name="text">The warning, or <see langword="null"/> when the box has none.</param>
    /// <param name="danger">Whether the combination is blocked.</param>
    public StyledText? Warning(string? text, bool danger)
    {
        var urgency = danger ? AnnouncementUrgency.Assertive : AnnouncementUrgency.Polite;
        _warned = Say(_warning, WarningAnnouncer, _warned, text, urgency);
        return text is null ? null : _warning;
    }

    /// <summary>
    /// The text of the recording <paramref name="text"/>, taken out of the box it was in and read out when it starts or
    /// changes.
    /// </summary>
    /// <param name="text">What the window waits for, or <see langword="null"/> when it does not listen.</param>
    public StyledText? Recording(string? text)
    {
        _listened = Say(
            _recording,
            RecordingAnnouncer,
            _listened,
            text,
            AnnouncementUrgency.Polite
        );
        return text is null ? null : _recording;
    }

    private static string? Say(
        StyledText line,
        LiveAnnouncer announcer,
        string? said,
        string? text,
        AnnouncementUrgency urgency
    )
    {
        if (line.Parent is Panel panel)
        {
            panel.Children.Remove(line);
        }

        // The box spaces its parts with margins; the line takes the ones of the box it goes into.
        line.Margin = default;
        if (text is null)
        {
            return null;
        }

        AutomationProperties.SetLiveSetting(
            line,
            urgency == AnnouncementUrgency.Assertive
                ? AutomationLiveSetting.Assertive
                : AutomationLiveSetting.Polite
        );
        line.Text = Parts.Unbroken(text);
        if (!string.Equals(said, text, StringComparison.Ordinal))
        {
            announcer.Say(text, urgency);
        }

        return text;
    }
}
