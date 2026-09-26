using System.Collections.Frozen;

namespace Clicalo.Platform.Windows.SysEvents;

/// <summary>
/// Foreground windows that are not apps the user works in (blueprint §7.9): the shell (taskbar, desktop, Start,
/// search, Alt+Tab, notification area), the touch keyboard and Voice access. <see cref="ForegroundMonitor"/> skips
/// them, so a tap on the taskbar or the touch keyboard does not change the external foreground Clícalo returns to.
/// These are Windows identifiers, not product text.
/// </summary>
internal static class NonAppWindows
{
    private static readonly FrozenSet<string> Classes = FrozenSet.Create(
        StringComparer.Ordinal,
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd",
        "Progman",
        "WorkerW",
        "NotifyIconOverflowWindow",
        "TopLevelWindowForOverflowXamlIsland",
        "XamlExplorerHostIslandWindow",
        "MultitaskingViewFrame",
        "TaskListThumbnailWnd",
        "ForegroundStaging",
        "Shell_InputSwitchTopLevelWindow",
        "IPTip_Main_Window"
    );

    private static readonly FrozenSet<string> Images = FrozenSet.Create(
        StringComparer.OrdinalIgnoreCase,
        "ShellExperienceHost.exe",
        "StartMenuExperienceHost.exe",
        "SearchHost.exe",
        "SearchApp.exe",
        "ShellHost.exe",
        "TextInputHost.exe",
        "TabTip.exe",
        "VoiceAccess.exe",
        "LockApp.exe"
    );

    /// <summary>True when a window of class <paramref name="className"/> in <paramref name="imageFileName"/> is not an app.</summary>
    public static bool Contains(string className, string? imageFileName) =>
        Classes.Contains(className)
        || (imageFileName is not null && Images.Contains(imageFileName));
}
