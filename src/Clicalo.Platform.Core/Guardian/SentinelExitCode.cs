namespace Clicalo.Platform.Core.Guardian;

/// <summary>Exit codes of Sentinel.</summary>
public enum SentinelExitCode
{
    /// <summary>The main process exited with code 0; whatever was still down was released and nothing relaunched.</summary>
    CleanExit = 0,

    /// <summary>The main process ended abnormally; everything down was released and the app relaunched.</summary>
    ReleasedAndRelaunched = 1,

    /// <summary>
    /// The main process ended abnormally; everything down was released but it was not relaunched: the relaunch failed
    /// or the crash loop was passed.
    /// </summary>
    ReleasedWithoutRelaunch = 2,

    /// <summary>
    /// The arguments did not follow <see cref="SentinelStartInfo"/>, or the parent handle cannot be waited on.
    /// </summary>
    InvalidArguments = 3,
}
