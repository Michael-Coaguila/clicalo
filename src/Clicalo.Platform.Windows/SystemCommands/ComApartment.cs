using Windows.Win32;
using Windows.Win32.System.Com;

namespace Clicalo.Platform.Windows.SystemCommands;

/// <summary>
/// COM for one call: joins the multithreaded apartment when the thread has none, and leaves it afterwards. On an STA
/// thread (the Shell thread) the existing apartment is used as it is.
/// </summary>
internal readonly struct ComApartment : IDisposable
{
    private readonly bool _initialized;

    private ComApartment(bool initialized) => _initialized = initialized;

    /// <summary>Joins COM when the calling thread has not.</summary>
    public static unsafe ComApartment Enter() =>
        new(PInvoke.CoInitializeEx(null, COINIT.COINIT_MULTITHREADED).Succeeded);

    /// <inheritdoc />
    public void Dispose()
    {
        if (_initialized)
        {
            PInvoke.CoUninitialize();
        }
    }
}
