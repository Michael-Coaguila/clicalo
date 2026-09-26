namespace Clicalo.Application.Localization;

/// <summary>A piece of a <see cref="MessageTemplate"/>: literal text or a named placeholder.</summary>
/// <param name="Text">The literal text, or the placeholder name without braces.</param>
/// <param name="IsPlaceholder">True for a placeholder.</param>
internal readonly record struct TemplateSegment(string Text, bool IsPlaceholder);
