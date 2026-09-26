namespace Clicalo.Generators.Localization;

/// <summary>A file of <c>data/i18n</c> as read by the caller (the generator or <c>cl i18n-check</c>).</summary>
internal sealed class LocalizationDataFile(string path, string? text)
{
    /// <summary>Path used in issues; only its file name decides the role of the file.</summary>
    public string Path { get; } = path;

    /// <summary>Content, or <c>null</c> when the file could not be read.</summary>
    public string? Text { get; } = text;
}
