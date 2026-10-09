using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.System.Variant;
using Windows.Win32.System.Wmi;

namespace Clicalo.Platform.Windows.SystemCommands;

/// <summary>
/// The brightness of the built-in screen through WMI (<c>root\WMI</c>, <c>WmiMonitorBrightness</c> and
/// <c>WmiMonitorBrightnessMethods.WmiSetBrightness</c>, EJE-016). A computer whose screen does not expose it (a desktop
/// with an external monitor) has no instance, so <see cref="IsSupported"/> is false and the library hides the two
/// actions. Each call joins COM for its own duration when its thread has not; every failure answers false.
/// </summary>
internal static unsafe class WmiBrightness
{
    /// <summary>How much one action changes the brightness, in percentage points.</summary>
    public const int StepPercent = 10;

    private const string Namespace = "ROOT\\WMI";
    private const string QueryLanguage = "WQL";
    private const string CurrentQuery =
        "SELECT CurrentBrightness FROM WmiMonitorBrightness WHERE Active=TRUE";
    private const string MethodsQuery =
        "SELECT * FROM WmiMonitorBrightnessMethods WHERE Active=TRUE";
    private const string MethodsClass = "WmiMonitorBrightnessMethods";
    private const string SetMethod = "WmiSetBrightness";
    private const uint WinNtAuthentication = 10;
    private const uint NoAuthorization = 0;
    private const int Infinite = -1;

    private static readonly Lazy<bool> Supported = new(() => ReadCurrent() is not null);

    /// <summary>Whether the built-in screen's brightness can be set (read once).</summary>
    public static bool IsSupported => Supported.Value;

    /// <summary>The brightness after a step of <paramref name="delta"/> points from <paramref name="current"/>.</summary>
    /// <param name="current">The current brightness, 0 to 100.</param>
    /// <param name="delta">The step, positive or negative.</param>
    public static byte Next(int current, int delta) => (byte)Math.Clamp(current + delta, 0, 100);

    /// <summary>Changes the brightness by <paramref name="delta"/> points; false when it cannot.</summary>
    /// <param name="delta">The step, positive or negative.</param>
    public static bool Step(int delta)
    {
        using var apartment = ComApartment.Enter();
        IWbemServices* services = null;
        try
        {
            services = Connect();
            if (services is null || Current(services) is not { } current)
            {
                return false;
            }

            return Set(services, Next(current, delta));
        }
        catch (Exception ex) when (IsComFailure(ex))
        {
            return false;
        }
        finally
        {
            Release(services);
        }
    }

    private static int? ReadCurrent()
    {
        using var apartment = ComApartment.Enter();
        IWbemServices* services = null;
        try
        {
            services = Connect();
            return services is null ? null : Current(services);
        }
        catch (Exception ex) when (IsComFailure(ex))
        {
            return null;
        }
        finally
        {
            Release(services);
        }
    }

    private static IWbemServices* Connect()
    {
        if (WbemLocator.CreateInstance<IWbemLocator>(out var locator).Failed)
        {
            return null;
        }

        var name = Bstr(Namespace);
        IWbemServices* services = null;
        try
        {
            locator->ConnectServer(name, default, default, default, 0, default, null, &services);
        }
        finally
        {
            PInvoke.SysFreeString(name);
            _ = locator->Release();
        }

        if (services is null)
        {
            return null;
        }

        if (
            PInvoke
                .CoSetProxyBlanket(
                    (IUnknown*)services,
                    WinNtAuthentication,
                    NoAuthorization,
                    default,
                    RPC_C_AUTHN_LEVEL.RPC_C_AUTHN_LEVEL_CALL,
                    RPC_C_IMP_LEVEL.RPC_C_IMP_LEVEL_IMPERSONATE,
                    null,
                    EOLE_AUTHENTICATION_CAPABILITIES.EOAC_NONE
                )
                .Failed
        )
        {
            Release(services);
            return null;
        }

        return services;
    }

    private static int? Current(IWbemServices* services)
    {
        var instance = First(services, CurrentQuery);
        if (instance is null)
        {
            return null;
        }

        try
        {
            VARIANT value = default;
            fixed (char* property = "CurrentBrightness")
            {
                instance->Get(property, 0, &value, null, null);
            }

            int? current = value.vt switch
            {
                VARENUM.VT_UI1 => value.bVal,
                VARENUM.VT_I4 => value.lVal,
                VARENUM.VT_UI4 => (int)value.ulVal,
                _ => null,
            };
            _ = PInvoke.VariantClear(&value);
            return current;
        }
        finally
        {
            Release(instance);
        }
    }

    private static bool Set(IWbemServices* services, byte brightness)
    {
        var methods = First(services, MethodsQuery);
        IWbemClassObject* methodsClass = null;
        IWbemClassObject* signature = null;
        IWbemClassObject* input = null;
        VARIANT path = default;
        var className = Bstr(MethodsClass);
        var method = Bstr(SetMethod);
        try
        {
            if (methods is null)
            {
                return false;
            }

            fixed (char* pathName = "__PATH")
            {
                methods->Get(pathName, 0, &path, null, null);
            }

            if (path.vt != VARENUM.VT_BSTR)
            {
                return false;
            }

            services->GetObject(
                className,
                WBEM_GENERIC_FLAG_TYPE.WBEM_FLAG_RETURN_WBEM_COMPLETE,
                null,
                &methodsClass,
                null
            );
            fixed (char* setName = SetMethod)
            {
                methodsClass->GetMethod(setName, 0, &signature, null);
            }

            signature->SpawnInstance(0, &input);
            VARIANT timeout = default;
            timeout.vt = VARENUM.VT_I4;
            timeout.lVal = 0;
            VARIANT level = default;
            level.vt = VARENUM.VT_UI1;
            level.bVal = brightness;
            fixed (char* timeoutName = "Timeout")
            fixed (char* levelName = "Brightness")
            {
                input->Put(timeoutName, 0, &timeout, 0);
                input->Put(levelName, 0, &level, 0);
            }

            services->ExecMethod(
                path.bstrVal,
                method,
                WBEM_GENERIC_FLAG_TYPE.WBEM_FLAG_RETURN_WBEM_COMPLETE,
                null,
                input,
                null,
                null
            );
            return true;
        }
        finally
        {
            _ = PInvoke.VariantClear(&path);
            PInvoke.SysFreeString(className);
            PInvoke.SysFreeString(method);
            Release(input);
            Release(signature);
            Release(methodsClass);
            Release(methods);
        }
    }

    private static IWbemClassObject* First(IWbemServices* services, string query)
    {
        var language = Bstr(QueryLanguage);
        var text = Bstr(query);
        IEnumWbemClassObject* results = null;
        try
        {
            services->ExecQuery(
                language,
                text,
                WBEM_GENERIC_FLAG_TYPE.WBEM_FLAG_FORWARD_ONLY
                    | WBEM_GENERIC_FLAG_TYPE.WBEM_FLAG_RETURN_IMMEDIATELY,
                null,
                &results
            );
            if (results is null)
            {
                return null;
            }

            IWbemClassObject* first = null;
            uint returned = 0;
            var found = results->Next(Infinite, 1, &first, &returned);
            return found.Succeeded && returned == 1 ? first : null;
        }
        finally
        {
            PInvoke.SysFreeString(language);
            PInvoke.SysFreeString(text);
            Release(results);
        }
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
