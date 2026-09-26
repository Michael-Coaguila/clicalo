namespace Clicalo.Domain.Privacy;

/// <summary>What a sensitive value is, which decides how it is redacted in logs and diagnostics (blueprint §9.4).</summary>
public enum RedactionKind
{
    /// <summary>Text the user typed or dictated.</summary>
    FreeText,

    /// <summary>A window title.</summary>
    WindowTitle,

    /// <summary>A search query.</summary>
    SearchQuery,

    /// <summary>A web address.</summary>
    Url,

    /// <summary>A file or folder path.</summary>
    FilePath,

    /// <summary>A secret (the text of a Text action, an API key).</summary>
    Secret,

    /// <summary>Keys captured while recording a combination.</summary>
    KeyCapture,
}
