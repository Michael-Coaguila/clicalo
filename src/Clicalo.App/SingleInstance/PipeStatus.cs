namespace Clicalo.App.SingleInstance;

/// <summary>The answer of the running instance (blueprint §3.4: <c>IpcResponse.Status</c>).</summary>
internal enum PipeStatus
{
    /// <summary>Done.</summary>
    Ok,

    /// <summary>Refused: malformed, too large, too frequent, or from another session.</summary>
    Rejected,

    /// <summary>A verb or protocol version this instance does not handle.</summary>
    Unsupported,
}
