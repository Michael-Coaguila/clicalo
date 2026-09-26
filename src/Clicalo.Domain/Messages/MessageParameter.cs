namespace Clicalo.Domain.Messages;

/// <summary>An argument a message expects: the placeholder name and its declared type.</summary>
/// <param name="Name">Placeholder name without braces (<c>count</c>).</param>
/// <param name="Type">Declared type in <c>data/i18n/placeholders.json</c>.</param>
public readonly record struct MessageParameter(string Name, MessageArgumentType Type);
