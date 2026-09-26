namespace Clicalo.Domain.Execution;

/// <summary>What an external effect waiting for its result is.</summary>
public enum PendingKind
{
    /// <summary>A web address opened on the Shell thread.</summary>
    Url,

    /// <summary>An app, Store app or document started on the Shell thread.</summary>
    App,

    /// <summary>A system command run on the Shell thread.</summary>
    System,

    /// <summary>A text put on the clipboard; <see cref="EngineEvent.ClipboardReady"/> sends Ctrl+V.</summary>
    Paste,
}
