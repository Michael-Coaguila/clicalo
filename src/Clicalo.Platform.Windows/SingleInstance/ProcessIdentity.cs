using System.Security.Principal;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;
using Windows.Win32.System.Threading;

namespace Clicalo.Platform.Windows.SingleInstance;

/// <summary>
/// Who a process is: its image, its user and its integrity level (blueprint §3.4, §7.9). Every query opens the process
/// with <c>PROCESS_QUERY_LIMITED_INFORMATION</c>, which works across integrity levels (an elevated app, an elevated
/// Clícalo), and answers <see langword="null"/> instead of failing (EC-PER-03). The single-instance pipe checks its
/// peer with it, and the foreground monitor names the app in front.
/// </summary>
public static unsafe class ProcessIdentity
{
    private const int MaxPath = 1024;

    /// <summary>The full path of the image of <paramref name="processId"/>, or <see langword="null"/>.</summary>
    /// <param name="processId">A process.</param>
    public static string? ImagePath(uint processId)
    {
        var process = Open(processId);
        if (process.IsNull)
        {
            return null;
        }

        try
        {
            var buffer = stackalloc char[MaxPath];
            var length = (uint)MaxPath;
            return PInvoke.QueryFullProcessImageName(
                process,
                PROCESS_NAME_FORMAT.PROCESS_NAME_WIN32,
                new PWSTR(buffer),
                &length
            )
                ? new string(buffer, 0, (int)length)
                : null;
        }
        finally
        {
            _ = PInvoke.CloseHandle(process);
        }
    }

    /// <summary>The file name of the image (<c>winword.exe</c>), or <see langword="null"/>.</summary>
    /// <param name="processId">A process.</param>
    public static string? ImageFileName(uint processId) =>
        ImagePath(processId) is { } path ? Path.GetFileName(path) : null;

    /// <summary>The user the process runs as, or <see langword="null"/> when its token cannot be read.</summary>
    /// <param name="processId">A process.</param>
    public static SecurityIdentifier? User(uint processId) =>
        WithToken(
            processId,
            TOKEN_INFORMATION_CLASS.TokenUser,
            static buffer => new SecurityIdentifier((nint)((TOKEN_USER*)buffer)->User.Sid.Value)
        );

    /// <summary>
    /// The mandatory integrity level of the process (the last sub-authority of its label: <c>0x2000</c> medium,
    /// <c>0x3000</c> high), or <see langword="null"/> when its token cannot be read.
    /// </summary>
    /// <param name="processId">A process.</param>
    public static uint? IntegrityLevel(uint processId) =>
        WithToken<uint?>(
            processId,
            TOKEN_INFORMATION_CLASS.TokenIntegrityLevel,
            static buffer =>
            {
                var sid = ((TOKEN_MANDATORY_LABEL*)buffer)->Label.Sid;
                var count = *PInvoke.GetSidSubAuthorityCount(sid);
                return count == 0 ? null : *PInvoke.GetSidSubAuthority(sid, (uint)(count - 1));
            }
        );

    /// <summary>Whether <paramref name="level"/> is High integrity or above.</summary>
    /// <param name="level">An integrity level read by <see cref="IntegrityLevel"/>.</param>
    public static bool IsElevated(uint level) => level >= PInvoke.SECURITY_MANDATORY_HIGH_RID;

    private static HANDLE Open(uint processId) =>
        processId == 0
            ? default
            : PInvoke.OpenProcess(
                PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION,
                false,
                processId
            );

    private static T? WithToken<T>(
        uint processId,
        TOKEN_INFORMATION_CLASS information,
        Func<nint, T> read
    )
    {
        var process = Open(processId);
        if (process.IsNull)
        {
            return default;
        }

        HANDLE token = default;
        try
        {
            if (!PInvoke.OpenProcessToken(process, TOKEN_ACCESS_MASK.TOKEN_QUERY, &token))
            {
                return default;
            }

            uint needed = 0;
            _ = PInvoke.GetTokenInformation(token, information, null, 0, &needed);
            if (needed == 0)
            {
                return default;
            }

            var buffer = stackalloc byte[(int)needed];
            return PInvoke.GetTokenInformation(token, information, buffer, needed, &needed)
                ? read((nint)buffer)
                : default;
        }
        finally
        {
            if (!token.IsNull)
            {
                _ = PInvoke.CloseHandle(token);
            }

            _ = PInvoke.CloseHandle(process);
        }
    }
}
