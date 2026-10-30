using System.Collections.Frozen;

namespace Clicalo.Platform.Windows.SysEvents;

/// <summary>
/// Foreground windows that are not apps the user works in (blueprint §7.9): the shell (taskbar, desktop, Start,
/// search, Alt+Tab, notification area), the touch keyboard and Voice access. <see cref="ForegroundMonitor"/> skips
/// them, so a tap on the taskbar or the touch keyboard does not change the external foreground Clícalo returns to.
/// Of <c>explorer.exe</c> only the folder windows of File Explorer are an app (CAT-007): its desktop, its taskbar, its
/// «Run» box and its progress dialogs never bring the Explorer profile into view. These are Windows identifiers, not
/// product text.
/// </summary>
internal static class NonAppWindows
{
    private const string ExplorerImage = "explorer.exe";

    /// <summary>The classes of a File Explorer folder window (<c>CabinetWClass</c>; <c>ExploreWClass</c> with the tree).</summary>
    private static readonly FrozenSet<string> FolderClasses = FrozenSet.Create(
        StringComparer.Ordinal,
        "CabinetWClass",
        "ExploreWClass"
    );

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
        || (imageFileName is not null && Images.Contains(imageFileName))
        || (IsExplorer(imageFileName) && !FolderClasses.Contains(className));

    /// <summary>True when <paramref name="imageFileName"/> is the Windows shell, which also hosts File Explorer.</summary>
    public static bool IsExplorer(string? imageFileName) =>
        string.Equals(imageFileName, ExplorerImage, StringComparison.OrdinalIgnoreCase);
}
