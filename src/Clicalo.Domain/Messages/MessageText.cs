namespace Clicalo.Domain.Messages;

/// <summary>
/// The value of a text placeholder: verbatim text (user data, such as a profile name) or a nested
/// <see cref="Messages.Message"/> (such as the localized name of the General profile). A nested message is
/// localized when the outer one is painted, so a language change re-localizes it too (IDI-001).
/// </summary>
public readonly record struct MessageText
{
    private MessageText(string? text, Message? message)
    {
        Text = text;
        Message = message;
    }

    /// <summary>The verbatim text, or <c>null</c> when this holds a message.</summary>
    public string? Text { get; }

    /// <summary>The nested message, or <c>null</c> when this holds verbatim text.</summary>
    public Message? Message { get; }

    /// <summary>Converts verbatim text.</summary>
    public static implicit operator MessageText(string text) => FromString(text);

    /// <summary>Converts a nested message.</summary>
    public static implicit operator MessageText(Message message) => FromMessage(message);

    /// <summary>Verbatim text, shown as is.</summary>
    public static MessageText FromString(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new MessageText(text, null);
    }

    /// <summary>A nested message, localized in the language of the outer message.</summary>
    public static MessageText FromMessage(Message message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return new MessageText(null, message);
    }
}
