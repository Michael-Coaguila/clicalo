namespace Clicalo.Application.Foreground;

/// <summary>Why a lease was denied (blueprint §3.6). Every denial ends in a live notice for the user.</summary>
public enum ForegroundDenialReason
{
    /// <summary>
    /// Windows refused the foreground after every step of the ladder for the origin. For a voice origin the notice
    /// offers the configurable global shortcut.
    /// </summary>
    RightsRefused,

    /// <summary>The target window no longer exists or is not visible.</summary>
    TargetUnavailable,

    /// <summary>A <see cref="LeaseKind.TrayMenu"/> lease is active and has priority.</summary>
    TrayMenuActive,

    /// <summary>The user switched apps (the foreground epoch changed) while the request was in progress.</summary>
    ForegroundChanged,
}
