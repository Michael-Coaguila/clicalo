namespace Clicalo.Domain.Library;

/// <summary>
/// Why a shortcut is incomplete (ATJ-009): it can be saved and undone, the editor marks it and the engine refuses
/// to run it (EJE-015).
/// </summary>
public enum CompletenessIssue
{
    /// <summary>Complete.</summary>
    None,

    /// <summary>A Tap, Hold or Toggle without keys.</summary>
    MissingKeys,

    /// <summary>A Text action without text.</summary>
    MissingText,

    /// <summary>A text that could not be decrypted on this machine (COP-005).</summary>
    TextUnavailable,

    /// <summary>A macro without steps.</summary>
    MissingSteps,

    /// <summary>A web action whose address is not a valid http or https address.</summary>
    InvalidAddress,

    /// <summary>An app action that cannot be started safely as written.</summary>
    InvalidApp,
}
