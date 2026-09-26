namespace Clicalo.Domain.Messages;

/// <summary>A named argument of a <see cref="Message"/>; the name is the placeholder without braces.</summary>
/// <param name="Name">Placeholder name (<c>count</c>).</param>
/// <param name="Value">Its value.</param>
public readonly record struct MessageArgument(string Name, MessageValue Value)
{
    /// <summary>Argument of a text placeholder: verbatim text or a nested message.</summary>
    public static MessageArgument Text(string name, MessageText value) =>
        new(name, MessageValue.FromText(value));

    /// <summary>Argument of an integer placeholder.</summary>
    public static MessageArgument WholeNumber(string name, long value) =>
        new(name, MessageValue.FromWholeNumber(value));

    /// <summary>Argument of a number placeholder.</summary>
    public static MessageArgument DecimalNumber(string name, decimal value) =>
        new(name, MessageValue.FromDecimalNumber(value));
}
