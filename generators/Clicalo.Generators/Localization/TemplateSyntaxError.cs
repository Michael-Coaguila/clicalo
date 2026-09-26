namespace Clicalo.Generators.Localization;

/// <summary>Why a text is not a valid template, and where (index in the decoded string).</summary>
internal readonly struct TemplateSyntaxError(string message, int index)
{
    public string Message { get; } = message;

    public int Index { get; } = index;
}
