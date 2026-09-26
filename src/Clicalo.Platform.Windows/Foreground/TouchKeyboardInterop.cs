using System.Runtime.InteropServices;
using Clicalo.Domain.Geometry;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.System.WinRT;
using Windows.Win32.UI.Shell;

namespace Clicalo.Platform.Windows.Foreground;

/// <summary>
/// The COM surface of the touch keyboard (blueprint §3.6 step 2): <c>IFrameworkInputPane::Location</c> for the area it
/// covers, <c>IInputPaneInterop</c> → <c>IInputPane2::TryShow/TryHide</c> on Windows 11, and the undocumented but
/// stable <c>ITipInvocation::Toggle</c> of TabTip on Windows 10. Every call initializes COM for its own duration
/// when the calling thread has not, and answers false or empty instead of throwing.
/// </summary>
internal static unsafe class TouchKeyboardInterop
{
    private const string InputPaneClass = "Windows.UI.ViewManagement.InputPane";
    private const int Windows11Build = 22000;
    private const int TryShowSlot = 6;
    private const int TryHideSlot = 7;
    private const int ToggleSlot = 3;

    private static readonly Guid InputPane2Interface = new("8A6B3F26-7090-4793-944C-C3F2CDE26276");
    private static readonly Guid TipInvocationClass = new("4CE576FA-83DC-4F88-951C-9D0782B4E376");
    private static readonly Guid TipInvocationInterface = new(
        "37C994E7-432B-4834-A2F7-DCE1F13B834B"
    );

    /// <summary>True from Windows 11, where <c>InputPane</c> can be driven for a desktop window.</summary>
    public static bool HasInputPaneForWindows =>
        Environment.OSVersion.Version.Build >= Windows11Build;

    /// <summary>The screen rectangle the touch keyboard covers, in physical pixels; empty when hidden.</summary>
    public static PhysicalRect Location()
    {
        using var apartment = ComApartment.Enter();
        if (FrameworkInputPane.CreateInstance<IFrameworkInputPane>(out var pane).Failed)
        {
            return PhysicalRect.Empty;
        }

        try
        {
            RECT area;
            pane->Location(&area);
            return PhysicalRect.FromEdges(area.left, area.top, area.right, area.bottom);
        }
        catch (COMException)
        {
            return PhysicalRect.Empty;
        }
        finally
        {
            _ = pane->Release();
        }
    }

    /// <summary><c>InputPane.TryShow</c> (or <c>TryHide</c>) for <paramref name="window"/>; false when unavailable.</summary>
    public static bool TryShowOrHide(nint window, bool show)
    {
        using var apartment = ComApartment.Enter();
        HSTRING className;
        fixed (char* name = InputPaneClass)
        {
            if (PInvoke.WindowsCreateString(name, (uint)InputPaneClass.Length, &className).Failed)
            {
                return false;
            }
        }

        IInputPaneInterop* interop = null;
        void* pane = null;
        try
        {
            var interopId = IInputPaneInterop.IID_Guid;
            if (PInvoke.RoGetActivationFactory(className, &interopId, (void**)&interop).Failed)
            {
                return false;
            }

            var paneId = InputPane2Interface;
            pane = interop->GetForWindow((HWND)window, &paneId);
            byte accepted = 0;
            var method = (delegate* unmanaged[Stdcall]<void*, byte*, HRESULT>)(
                (*(void***)pane)[show ? TryShowSlot : TryHideSlot]
            );
            return method(pane, &accepted).Succeeded && accepted != 0;
        }
        catch (COMException)
        {
            return false;
        }
        finally
        {
            if (pane is not null)
            {
                _ = ((IUnknown*)pane)->Release();
            }

            if (interop is not null)
            {
                _ = interop->Release();
            }

            _ = PInvoke.WindowsDeleteString(className);
        }
    }

    /// <summary><c>ITipInvocation::Toggle</c>: shows the keyboard when hidden and hides it when shown.</summary>
    public static bool Toggle()
    {
        using var apartment = ComApartment.Enter();
        var classId = TipInvocationClass;
        var interfaceId = TipInvocationInterface;
        void* tip = null;
        var created = PInvoke.CoCreateInstance(
            &classId,
            null,
            CLSCTX.CLSCTX_INPROC_HANDLER | CLSCTX.CLSCTX_LOCAL_SERVER,
            &interfaceId,
            &tip
        );
        if (created.Failed || tip is null)
        {
            // TabTip is not running: there is no touch keyboard to toggle.
            return false;
        }

        try
        {
            var toggle = (delegate* unmanaged[Stdcall]<void*, HWND, HRESULT>)(
                (*(void***)tip)[ToggleSlot]
            );
            return toggle(tip, PInvoke.GetDesktopWindow()).Succeeded;
        }
        finally
        {
            _ = ((IUnknown*)tip)->Release();
        }
    }

    /// <summary>
    /// COM for one call: joins the multithreaded apartment when the thread has none, and leaves it afterwards. On an
    /// STA thread (the UI thread) the existing apartment is used as it is.
    /// </summary>
    private readonly struct ComApartment : IDisposable
    {
        private readonly bool _initialized;

        private ComApartment(bool initialized) => _initialized = initialized;

        public static ComApartment Enter() =>
            new(PInvoke.CoInitializeEx(null, COINIT.COINIT_MULTITHREADED).Succeeded);

        public void Dispose()
        {
            if (_initialized)
            {
                PInvoke.CoUninitialize();
            }
        }
    }
}
