using Clicalo.UI.Wpf.Automation;

namespace Clicalo.UI.Wpf.Workspace.Internal;

/// <summary>
/// The lines a view draws again on every render that must still be heard (ACC-001, REG-06): «Probar» and its
/// question, a notice of a first tap, a question of a binding. Such a line is never the same element twice, so it can
/// never be a live region; its window says it instead, once when it appears and again only when its text changes. A
/// line that goes away is forgotten, so it is said again when it comes back.
/// </summary>
/// <param name="say">The single way of the window to say a text (<see cref="LiveAnnouncer.Say"/> of its window).</param>
internal sealed class SpokenLines(Action<string, AnnouncementUrgency> say)
{
    private readonly Dictionary<string, string> _said = new(StringComparer.Ordinal);

    /// <summary>
    /// The line <paramref name="line"/> shows <paramref name="text"/> now: it is said when it was not on show with that
    /// text.
    /// </summary>
    /// <param name="line">Which line of the view (its own name, never shown).</param>
    /// <param name="text">The text on show, or <see langword="null"/> or empty when the line is not on show.</param>
    /// <param name="urgency">Polite, or assertive for an error.</param>
    public void Show(
        string line,
        string? text,
        AnnouncementUrgency urgency = AnnouncementUrgency.Polite
    )
    {
        if (string.IsNullOrEmpty(text))
        {
            _ = _said.Remove(line);
            return;
        }

        if (
            _said.TryGetValue(line, out var said)
            && string.Equals(said, text, StringComparison.Ordinal)
        )
        {
            return;
        }

        _said[line] = text;
        say(text, urgency);
    }

    /// <summary>Forgets every line: another shortcut or another step is on show.</summary>
    public void Clear() => _said.Clear();
}
