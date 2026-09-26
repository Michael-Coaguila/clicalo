using System.IO;

namespace Clicalo.App.Interop;

/// <summary>
/// The image path of a process with <c>PROCESS_QUERY_LIMITED_INFORMATION</c>, which works across integrity levels
/// (an elevated app, an elevated Clícalo). Answers <see langword="null"/> instead of failing (EC-PER-03).
/// </summary>
internal static class ProcessImages
{
    private const int MaxPath = 1024;

    /// <summary>The full path of the image of <paramref name="processId"/>, or <see langword="null"/>.</summary>
    public static string? PathOf(uint processId)
    {
        using var process = NativeMethods.OpenProcess(
            NativeMethods.ProcessQueryLimitedInformation,
            inheritHandle: false,
            processId
        );
        if (process.IsInvalid)
        {
            return null;
        }

        var buffer = new char[MaxPath];
        var length = (uint)buffer.Length;
        return NativeMethods.QueryFullProcessImageName(process, 0, buffer, ref length)
            ? new string(buffer, 0, (int)length)
            : null;
    }

    /// <summary>The file name of the image (<c>winword.exe</c>), or <see langword="null"/>.</summary>
    public static string? FileNameOf(uint processId) =>
        PathOf(processId) is { } path ? Path.GetFileName(path) : null;
}
