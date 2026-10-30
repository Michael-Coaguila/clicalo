using System.Security;
using Clicalo.Application.Ports;

namespace Clicalo.Platform.Windows.Startup;

/// <summary>
/// «Iniciar con Windows» (SIS-002, ADR-0027): the value <c>Clicalo</c> of the user's <c>Run</c> key with the installed
/// executable, quoted and without arguments. It never starts elevated: «Reabrir como administrador» is on demand, with
/// the UAC prompt every time (user decision D7). A copy that is not the installed one (a development build) never
/// registers itself.
/// </summary>
public sealed class StartupRegistration : IStartupRegistration
{
    /// <summary>The name of the value in the <c>Run</c> key.</summary>
    public const string ValueName = "Clicalo";

    private readonly string? _executable;
    private readonly IRunKey _key;

    /// <summary>Registers <paramref name="installedExecutable"/> in the user's <c>Run</c> key.</summary>
    /// <param name="installedExecutable">The installed <c>Clicalo.exe</c>, or null for a copy that is not installed.</param>
    public StartupRegistration(string? installedExecutable)
        : this(installedExecutable, new CurrentUserRunKey()) { }

    /// <summary>Registers over another key (tests).</summary>
    internal StartupRegistration(string? installedExecutable, IRunKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        _executable = installedExecutable;
        _key = key;
    }

    /// <inheritdoc />
    public bool IsAvailable => _executable is not null;

    /// <inheritdoc />
    public bool IsEnabled
    {
        get
        {
            try
            {
                return _executable is not null
                    && string.Equals(
                        _key.Read(ValueName),
                        Command(_executable),
                        StringComparison.OrdinalIgnoreCase
                    );
            }
            catch (Exception ex) when (IsRefusal(ex))
            {
                return false;
            }
        }
    }

    /// <summary>The command line of the value: the executable, quoted, without arguments.</summary>
    /// <param name="executable">The installed executable.</param>
    public static string Command(string executable) => "\"" + executable + "\"";

    /// <summary>
    /// Removes the value whatever it points to: the uninstaller runs it (Velopack's hook), so no entry is left pointing
    /// to a deleted file. Never throws.
    /// </summary>
    public static void RemoveForUninstall()
    {
        try
        {
            new CurrentUserRunKey().Delete(ValueName);
        }
        catch (Exception ex) when (IsRefusal(ex))
        {
            // Uninstalling goes on: a stale entry only fails to start a missing file.
        }
    }

    /// <inheritdoc />
    public bool TrySetEnabled(bool enabled)
    {
        if (_executable is null)
        {
            return false;
        }

        try
        {
            if (enabled)
            {
                _key.Write(ValueName, Command(_executable));
            }
            else
            {
                _key.Delete(ValueName);
            }

            return true;
        }
        catch (Exception ex) when (IsRefusal(ex))
        {
            return false;
        }
    }

    private static bool IsRefusal(Exception exception) =>
        exception is UnauthorizedAccessException or SecurityException or IOException;
}
