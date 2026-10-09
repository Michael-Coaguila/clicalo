using System.Collections.Frozen;
using Clicalo.Domain.Library;

namespace Clicalo.Domain.Execution;

/// <summary>
/// What the Shell thread may start (EJE-011, LOG-008): only http and https addresses, and apps, Store apps or documents
/// that never go through a command interpreter or a script host. A network path (UNC) starts only when the shortcut asks
/// for confirmation, whose second tap is the confirmation EJE-011 requires. Pure; the launcher checks again.
/// </summary>
public static class LaunchSafety
{
    private const char Backslash = '\\';
    private const char Slash = '/';
    private const string LongUncPrefix = "\\\\?\\UNC\\";
    private const string LongPathPrefix = "\\\\?\\";
    private const string DevicePrefix = "\\\\.\\";

    private static readonly FrozenSet<string> Interpreters = new[]
    {
        "cmd",
        "powershell",
        "powershell_ise",
        "pwsh",
        "wscript",
        "cscript",
        "mshta",
        "bash",
        "wsl",
        "wslhost",
        "sh",
        "rundll32",
        "regsvr32",
        "msiexec",
        "forfiles",
        "conhost",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> ScriptExtensions = new[]
    {
        ".bat",
        ".cmd",
        ".ps1",
        ".psm1",
        ".psd1",
        ".vbs",
        ".vbe",
        ".js",
        ".jse",
        ".wsf",
        ".wsh",
        ".hta",
        ".msc",
        ".scr",
        ".pif",
        ".com",
        ".reg",
        ".inf",
        ".sct",
        ".cpl",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether <paramref name="request"/> may start.</summary>
    /// <param name="request">What the action starts.</param>
    /// <param name="confirmed">The shortcut asks for confirmation (a second tap), which allows a network path.</param>
    public static LaunchVerdict Check(LaunchRequest request, bool confirmed)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request switch
        {
            LaunchRequest.OpenUrl url => url.Address.IsAbsoluteUri
            && url.Address.Scheme is "http" or "https"
                ? LaunchVerdict.Allowed
                : LaunchVerdict.Invalid,
            LaunchRequest.StartApp { Target: AppTarget.Executable exe } => File(
                exe.Path,
                confirmed,
                executable: true
            ),
            LaunchRequest.StartApp { Target: AppTarget.Document document } => File(
                document.Path,
                confirmed,
                executable: false
            ),
            LaunchRequest.StartApp { Target: AppTarget.StoreApp store } =>
                string.IsNullOrWhiteSpace(store.AppUserModelId)
                || store.AppUserModelId.Contains(Backslash, StringComparison.Ordinal)
                    ? LaunchVerdict.Invalid
                    : LaunchVerdict.Allowed,
            _ => LaunchVerdict.Invalid,
        };
    }

    /// <summary>
    /// Whether <paramref name="path"/> is a network path: two leading separators (a UNC share), or the long form of a
    /// UNC share; the local device and long-path prefixes are not.
    /// </summary>
    /// <param name="path">The path.</param>
    public static bool IsNetworkPath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var trimmed = path.Trim().Trim('"').Replace(Slash, Backslash);
        if (trimmed.StartsWith(LongUncPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (
            trimmed.StartsWith(LongPathPrefix, StringComparison.Ordinal)
            || trimmed.StartsWith(DevicePrefix, StringComparison.Ordinal)
        )
        {
            return false;
        }

        return trimmed.Length >= 2 && trimmed[0] == Backslash && trimmed[1] == Backslash;
    }

    private static LaunchVerdict File(string path, bool confirmed, bool executable)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return LaunchVerdict.Invalid;
        }

        var trimmed = path.Trim().Trim('"');
        if (IsNetworkPath(trimmed) && !confirmed)
        {
            return LaunchVerdict.NetworkPath;
        }

        var name = trimmed[(trimmed.LastIndexOfAny([Backslash, Slash]) + 1)..];
        var dot = name.LastIndexOf('.');
        var extension = dot >= 0 ? name[dot..] : string.Empty;
        var stem = dot >= 0 ? name[..dot] : name;
        if (ScriptExtensions.Contains(extension))
        {
            return LaunchVerdict.Interpreter;
        }

        return executable && Interpreters.Contains(stem)
            ? LaunchVerdict.Interpreter
            : LaunchVerdict.Allowed;
    }
}
