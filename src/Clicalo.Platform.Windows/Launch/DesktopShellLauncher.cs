using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.System.Variant;
using Windows.Win32.UI.Shell;
using ComServiceProvider = Windows.Win32.System.Com.IServiceProvider;

namespace Clicalo.Platform.Windows.Launch;

/// <summary>
/// Starts a file through the desktop shell's own <c>IShellDispatch2::ShellExecute</c> (blueprint §3.3, rule 3), so an
/// elevated Clícalo opens apps and webs with the user's normal, unelevated token (EJE-011): ShellWindows → the desktop
/// window's <c>IShellBrowser</c> → its active <c>IShellView</c> → the background <c>IShellFolderViewDual</c> → its
/// <c>Application</c>. Runs on the Shell thread (STA). Never elevates and never uses an interpreter; answers false when
/// the desktop shell is not there (Explorer not running).
/// </summary>
internal static unsafe class DesktopShellLauncher
{
    // IShellBrowser and IShellView are not generated (their layout depends on the CPU architecture in the metadata), so
    // their two calls go through the vtable: IShellBrowser::QueryActiveShellView and IShellView::GetItemObject.
    private const int QueryActiveShellViewSlot = 15;
    private const int GetItemObjectSlot = 15;
    private const uint SvgioBackground = 0;
    private const int ShowNormal = 1;

    private static readonly Guid TopLevelBrowserService = new(
        "4C96BE40-915C-11CF-99D3-00AA004AE837"
    );
    private static readonly Guid ShellBrowserInterface = new(
        "000214E2-0000-0000-C000-000000000046"
    );
    private static readonly Guid ShellFolderViewDualInterface = new(
        "E7A1AF80-4D96-11CF-960C-0080C7F4EE85"
    );
    private static readonly Guid ShellDispatch2Interface = new(
        "A4C6892C-3BA9-11D2-9DEA-00C04FB16162"
    );

    /// <summary>Starts <paramref name="file"/> with <paramref name="arguments"/> as the desktop shell would.</summary>
    /// <param name="file">An executable, a document, an address or a <c>shell:</c> path.</param>
    /// <param name="arguments">Arguments, passed as they are; empty for none.</param>
    /// <param name="directory">The working directory; empty for the shell's default.</param>
    [SuppressMessage(
        "ApiDesign",
        "RS0030:Do not use banned APIs",
        Justification = "ILauncher: the unelevated launch through the desktop shell's IShellDispatch2 (EJE-011, §3.3 rule 3)."
    )]
    public static bool TryStart(string file, string arguments, string directory)
    {
        IShellWindows* windows = null;
        IDispatch* desktop = null;
        ComServiceProvider* provider = null;
        void* browser = null;
        void* view = null;
        IDispatch* background = null;
        IShellFolderViewDual* folderView = null;
        IDispatch* application = null;
        IShellDispatch2* shell = null;
        try
        {
            if (ShellWindows.CreateInstance(out windows).Failed)
            {
                return false;
            }

            VARIANT location = default;
            location.vt = VARENUM.VT_I4;
            location.lVal = 0; // CSIDL_DESKTOP
            VARIANT root = default;
            int window;
            desktop = windows->FindWindowSW(
                &location,
                &root,
                ShellWindowTypeConstants.SWC_DESKTOP,
                &window,
                ShellWindowFindWindowOptions.SWFO_NEEDDISPATCH
            );
            if (desktop is null)
            {
                return false;
            }

            var providerId = ComServiceProvider.IID_Guid;
            if (desktop->QueryInterface(&providerId, (void**)&provider).Failed)
            {
                return false;
            }

            var service = TopLevelBrowserService;
            var browserId = ShellBrowserInterface;
            provider->QueryService(&service, &browserId, &browser);
            if (browser is null)
            {
                return false;
            }

            var activeView = (delegate* unmanaged[Stdcall]<void*, void**, HRESULT>)(
                (*(void***)browser)[QueryActiveShellViewSlot]
            );
            if (activeView(browser, &view).Failed || view is null)
            {
                return false;
            }

            var dispatchId = IDispatch.IID_Guid;
            var itemObject = (delegate* unmanaged[Stdcall]<void*, uint, Guid*, void**, HRESULT>)(
                (*(void***)view)[GetItemObjectSlot]
            );
            if (
                itemObject(view, SvgioBackground, &dispatchId, (void**)&background).Failed
                || background is null
            )
            {
                return false;
            }

            var folderViewId = ShellFolderViewDualInterface;
            if (background->QueryInterface(&folderViewId, (void**)&folderView).Failed)
            {
                return false;
            }

            application = folderView->Application;
            var shellId = ShellDispatch2Interface;
            if (application is null || application->QueryInterface(&shellId, (void**)&shell).Failed)
            {
                return false;
            }

            return Execute(shell, file, arguments, directory);
        }
        catch (Exception ex) when (IsComFailure(ex))
        {
            return false;
        }
        finally
        {
            Release(shell);
            Release(application);
            Release(folderView);
            Release(background);
            Release(view);
            Release(browser);
            Release(provider);
            Release(desktop);
            Release(windows);
        }
    }

    [SuppressMessage(
        "ApiDesign",
        "RS0030:Do not use banned APIs",
        Justification = "ILauncher: IShellDispatch2::ShellExecute of the desktop shell, unelevated (EJE-011, §3.3 rule 3)."
    )]
    private static bool Execute(
        IShellDispatch2* shell,
        string file,
        string arguments,
        string directory
    )
    {
        var fileText = Bstr(file);
        var argumentsVariant = Text(arguments);
        var directoryVariant = Text(directory);
        var operation = Text(string.Empty);
        VARIANT show = default;
        show.vt = VARENUM.VT_I4;
        show.lVal = ShowNormal;
        try
        {
            shell->ShellExecute(fileText, argumentsVariant, directoryVariant, operation, show);
            return true;
        }
        finally
        {
            PInvoke.SysFreeString(fileText);
            _ = PInvoke.VariantClear(&argumentsVariant);
            _ = PInvoke.VariantClear(&directoryVariant);
            _ = PInvoke.VariantClear(&operation);
        }
    }

    private static VARIANT Text(string value)
    {
        VARIANT variant = default;
        variant.vt = VARENUM.VT_BSTR;
        variant.bstrVal = Bstr(value);
        return variant;
    }

    private static BSTR Bstr(string text)
    {
        fixed (char* chars = text)
        {
            return PInvoke.SysAllocString(chars);
        }
    }

    private static void Release(void* com)
    {
        if (com is not null)
        {
            _ = ((IUnknown*)com)->Release();
        }
    }

    private static bool IsComFailure(Exception exception) =>
        exception
            is COMException
                or UnauthorizedAccessException
                or NotImplementedException
                or ArgumentException
                or InvalidCastException
                or InvalidOperationException;
}
