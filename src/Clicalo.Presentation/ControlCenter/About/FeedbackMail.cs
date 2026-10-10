namespace Clicalo.Presentation.ControlCenter.About;

/// <summary>
/// The email of [Enviar por correo] (ACE-004): subject «[Clícalo] {kind}» and a body with the message and, after a
/// «—» line, the version line and the line of the log to attach when they are included. Pure.
/// </summary>
public static class FeedbackMail
{
    /// <summary>The body of the email.</summary>
    /// <param name="message">What the person wrote.</param>
    /// <param name="systemLine">The app and Windows versions, or <see langword="null"/> when not included.</param>
    /// <param name="logLine">The line of the log to attach, or <see langword="null"/> when not attached.</param>
    public static string Body(string message, string? systemLine, string? logLine)
    {
        ArgumentNullException.ThrowIfNull(message);
        var lines = new List<string> { message.Trim() };
        if (systemLine is not null || logLine is not null)
        {
            lines.Add(string.Empty);
            lines.Add("—");
        }

        if (systemLine is not null)
        {
            lines.Add(systemLine);
        }

        if (logLine is not null)
        {
            lines.Add(logLine);
        }

        return string.Join('\n', lines);
    }

    /// <summary>The <c>mailto:</c> address that opens the email app with the message ready.</summary>
    /// <param name="to">The recipient: the contact email of the project (<see cref="AboutLinks.Email"/>).</param>
    /// <param name="subject">The subject.</param>
    /// <param name="body">The body.</param>
    public static Uri Address(string to, string subject, string body)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(to);
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(body);
        return new Uri(
            Uri.UriSchemeMailto
                + ":"
                + to.Trim()
                + "?subject="
                + Uri.EscapeDataString(subject)
                + "&body="
                + Uri.EscapeDataString(body.Replace("\n", "\r\n", StringComparison.Ordinal))
        );
    }
}
