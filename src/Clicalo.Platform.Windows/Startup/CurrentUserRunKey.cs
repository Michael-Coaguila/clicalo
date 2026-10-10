using Microsoft.Win32;

namespace Clicalo.Platform.Windows.Startup;

/// <summary><c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c>: per user, no administrator needed.</summary>
internal sealed class CurrentUserRunKey : IRunKey
{
    /// <summary>The path of the key under <c>HKEY_CURRENT_USER</c>.</summary>
    public const string Path = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <inheritdoc />
    public string? Read(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(Path, writable: false);
        return key?.GetValue(name) as string;
    }

    /// <inheritdoc />
    public void Write(string name, string command)
    {
        using var key = Registry.CurrentUser.CreateSubKey(Path, writable: true);
        key.SetValue(name, command, RegistryValueKind.String);
    }

    /// <inheritdoc />
    public void Delete(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(Path, writable: true);
        key?.DeleteValue(name, throwOnMissingValue: false);
    }
}
