using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Clicalo.Windowing.IntegrationTests.Automation;

/// <summary>
/// Turns the Windows contrast theme on and off (<c>SPI_SETHIGHCONTRAST</c>). It changes a setting of the whole
/// session, so only <see cref="HighContrastTests"/> uses it, and only on a CI runner (<see cref="IsAllowed"/>):
/// never on a person's machine.
/// </summary>
internal static class SystemContrastSwitch
{
    private const uint SpiGetHighContrast = 0x0042;
    private const uint SpiSetHighContrast = 0x0043;
    private const uint HcfHighContrastOn = 0x0001;
    private const uint SpifSendChange = 0x0002;

    /// <summary>True only on a GitHub Actions runner (<c>CI=true</c> and <c>GITHUB_ACTIONS=true</c>).</summary>
    public static bool IsAllowed =>
        string.Equals(
            Environment.GetEnvironmentVariable("CI"),
            "true",
            StringComparison.OrdinalIgnoreCase
        )
        && string.Equals(
            Environment.GetEnvironmentVariable("GITHUB_ACTIONS"),
            "true",
            StringComparison.OrdinalIgnoreCase
        );

    /// <summary>Whether Windows reports a contrast theme on right now.</summary>
    public static bool IsOn
    {
        get
        {
            var contrast = Current();
            return (contrast.Flags & HcfHighContrastOn) != 0;
        }
    }

    /// <summary>Asks Windows to turn the contrast theme on or off and broadcast the change.</summary>
    /// <returns>False when Windows refused the request.</returns>
    public static bool TrySet(bool on)
    {
        if (!IsAllowed)
        {
            throw new InvalidOperationException(
                "The contrast theme is only switched on a CI runner, never on a person's machine."
            );
        }

        var contrast = Current();
        contrast.Flags = on
            ? contrast.Flags | HcfHighContrastOn
            : contrast.Flags & ~HcfHighContrastOn;
        return SystemParametersInfoW(
            SpiSetHighContrast,
            contrast.Size,
            ref contrast,
            SpifSendChange
        );
    }

    private static HighContrast Current()
    {
        var contrast = new HighContrast { Size = (uint)Marshal.SizeOf<HighContrast>() };
        if (!SystemParametersInfoW(SpiGetHighContrast, contrast.Size, ref contrast, 0))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        return contrast;
    }

    [DllImport("user32.dll", SetLastError = true, ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfoW(
        uint action,
        uint parameter,
        ref HighContrast value,
        uint update
    );

    [StructLayout(LayoutKind.Sequential)]
    private struct HighContrast
    {
        public uint Size;
        public uint Flags;
        public nint DefaultScheme;
    }
}
