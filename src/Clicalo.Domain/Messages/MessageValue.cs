using System.Diagnostics.CodeAnalysis;

namespace Clicalo.Domain.Messages;

/// <summary>The value of a message argument: verbatim text, a nested message, a whole number or a decimal number.</summary>
public readonly record struct MessageValue
{
    private readonly string? _text;
    private readonly Message? _message;
    private readonly long _integer;
    private readonly decimal _number;

    private MessageValue(
        MessageValueKind kind,
        string? text,
        Message? message,
        long integer,
        decimal number
    )
    {
        Kind = kind;
        _text = text;
        _message = message;
        _integer = integer;
        _number = number;
    }

    /// <summary>What this value holds.</summary>
    public MessageValueKind Kind { get; }

    /// <summary>Verbatim text or nested message.</summary>
    public static MessageValue FromText(MessageText value) =>
        value.Message is { } message
            ? new MessageValue(MessageValueKind.Message, null, message, 0, 0m)
            : new MessageValue(MessageValueKind.Text, value.Text ?? string.Empty, null, 0, 0m);

    /// <summary>A whole number.</summary>
    public static MessageValue FromWholeNumber(long value) =>
        new(MessageValueKind.WholeNumber, null, null, value, 0m);

    /// <summary>A decimal number; its scale is kept (<c>0.50m</c> is written with two decimals).</summary>
    public static MessageValue FromDecimalNumber(decimal value) =>
        new(MessageValueKind.DecimalNumber, null, null, 0, value);

    /// <summary>Gets the verbatim text when <see cref="Kind"/> is <see cref="MessageValueKind.Text"/>.</summary>
    public bool TryGetText([NotNullWhen(true)] out string? text)
    {
        text = Kind == MessageValueKind.Text ? _text ?? string.Empty : null;
        return text is not null;
    }

    /// <summary>Gets the nested message when <see cref="Kind"/> is <see cref="MessageValueKind.Message"/>.</summary>
    public bool TryGetMessage([NotNullWhen(true)] out Message? message)
    {
        message = Kind == MessageValueKind.Message ? _message : null;
        return message is not null;
    }

    /// <summary>Gets the number when <see cref="Kind"/> is <see cref="MessageValueKind.WholeNumber"/>.</summary>
    public bool TryGetWholeNumber(out long value)
    {
        value = _integer;
        return Kind == MessageValueKind.WholeNumber;
    }

    /// <summary>Gets the number when <see cref="Kind"/> is <see cref="MessageValueKind.DecimalNumber"/>.</summary>
    public bool TryGetDecimalNumber(out decimal value)
    {
        value = _number;
        return Kind == MessageValueKind.DecimalNumber;
    }
}
