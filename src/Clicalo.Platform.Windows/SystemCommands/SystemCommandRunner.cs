using Clicalo.Domain.Catalog;
using Windows.Win32;

namespace Clicalo.Platform.Windows.SystemCommands;

/// <summary>
/// The system actions that cannot be sent as keys (EJE-016, <c>data/catalogs/system-commands.json</c>): «Lock computer»
/// with <c>LockWorkStation</c>, which also replaces Win+L (EJE-014), and brightness up and down through WMI on the
/// computers whose screen allows it. Runs on the Shell thread; never throws.
/// </summary>
public static class SystemCommandRunner
{
    /// <summary>The «lock» command.</summary>
    public static SystemCommandId Lock { get; } = new("lock");

    /// <summary>The «brightness.up» command.</summary>
    public static SystemCommandId BrightnessUp { get; } = new("brightness.up");

    /// <summary>The «brightness.down» command.</summary>
    public static SystemCommandId BrightnessDown { get; } = new("brightness.down");

    /// <summary>Runs <paramref name="command"/>; false when it is unknown or Windows refused it.</summary>
    /// <param name="command">The command.</param>
    public static bool Run(SystemCommandId command)
    {
        if (command == Lock)
        {
            return PInvoke.LockWorkStation();
        }

        if (command == BrightnessUp)
        {
            return WmiBrightness.Step(WmiBrightness.StepPercent);
        }

        return command == BrightnessDown && WmiBrightness.Step(-WmiBrightness.StepPercent);
    }

    /// <summary>
    /// Whether this computer can run <paramref name="command"/> (the brightness of an external monitor or a desktop
    /// cannot be set this way): the library hides what is not available (EJE-016, <c>availability</c> of the catalog).
    /// </summary>
    /// <param name="command">The command.</param>
    public static bool IsAvailable(SystemCommandId command) =>
        command == Lock
        || ((command == BrightnessUp || command == BrightnessDown) && WmiBrightness.IsSupported);
}
