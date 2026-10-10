using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Clicalo.Platform.Windows.Tray;

/// <summary>
/// The two images of the tray icon (BUR-003, BUR-004): the icon of Clícalo, and the same at 55 % for the hidden panel
/// and the pause. Both are the files of <c>assets/icons</c>, drawn by <c>app-icon</c> of the developer CLI and
/// embedded in this assembly; each handle is created on first use at the size of the notification area and destroyed
/// with <see cref="Dispose"/>. Used from the SysEvents thread only.
/// </summary>
internal sealed class TrayIconImages : IDisposable
{
    /// <summary>The embedded icon of Clícalo.</summary>
    public const string NormalResource = "Clicalo.Platform.Windows.Tray.clicalo.ico";

    /// <summary>The embedded icon at 55 %.</summary>
    public const string DimResource = "Clicalo.Platform.Windows.Tray.clicalo-dim.ico";

    private const uint IconVersion = 0x00030000;

    private HICON _normal;
    private HICON _dim;

    /// <summary>The bytes of an embedded icon file, or an empty array when it is missing.</summary>
    /// <param name="resource"><see cref="NormalResource"/> or <see cref="DimResource"/>.</param>
    public static byte[] Read(string resource)
    {
        using var stream = typeof(TrayIconImages).Assembly.GetManifestResourceStream(resource);
        if (stream is null)
        {
            return [];
        }

        var bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }

    /// <summary>
    /// The icon to show; the stock application icon of Windows if the embedded one cannot be created, so the tray
    /// never loses its icon (BUR-005).
    /// </summary>
    /// <param name="dimmed">Whether the panel is hidden or Clícalo is paused.</param>
    public unsafe nint Handle(bool dimmed)
    {
        ref var handle = ref dimmed ? ref _dim : ref _normal;
        if (handle.IsNull)
        {
            handle = Create(Read(dimmed ? DimResource : NormalResource));
        }

        return handle.IsNull
            ? (nint)PInvoke.LoadIcon(default, PInvoke.IDI_APPLICATION).Value
            : (nint)handle.Value;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Destroy(ref _normal);
        Destroy(ref _dim);
    }

    private static unsafe HICON Create(byte[] file)
    {
        var side = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXSMICON);
        if (IconFileEntry.Best(file, side) is not { } entry)
        {
            return default;
        }

        fixed (byte* image = &file[entry.Offset])
        {
            return PInvoke.CreateIconFromResourceEx(
                image,
                (uint)entry.Length,
                true,
                IconVersion,
                side,
                side,
                IMAGE_FLAGS.LR_DEFAULTCOLOR
            );
        }
    }

    private static void Destroy(ref HICON handle)
    {
        if (!handle.IsNull)
        {
            _ = PInvoke.DestroyIcon(handle);
            handle = default;
        }
    }
}
