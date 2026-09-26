namespace Clicalo.Domain.Settings;

/// <summary>Startup and stability (docs/02 <c>reliability</c>, SIS-*).</summary>
/// <param name="StartWithWindows">Start when the user signs in.</param>
/// <param name="AutoBackup">Automatic backups (COP-*).</param>
/// <param name="CrashRecovery">Relaunch after a crash (Sentinel).</param>
/// <param name="SingleInstance">One instance per session (always on in 2.0, SIS-003).</param>
/// <param name="RunAsAdmin">Start elevated (SIS-002).</param>
public sealed record ReliabilitySettings(
    bool StartWithWindows,
    bool AutoBackup,
    bool CrashRecovery,
    bool SingleInstance,
    bool RunAsAdmin
);
