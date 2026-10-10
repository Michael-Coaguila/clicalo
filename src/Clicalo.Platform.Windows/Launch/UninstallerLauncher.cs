using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Clicalo.Platform.Windows.Elevation;

namespace Clicalo.Platform.Windows.Launch;

/// <summary>
/// Starts the uninstaller of the installed copy (NFR-010, proposal P6, ADR-0029): Velopack's own
/// <c>Update.exe --uninstall</c>, the same command the «Aplicaciones» page of Windows Settings runs. It only ever
/// starts <c>%LocalAppData%\Clicalo.App\Update.exe</c> when this process is exactly the installed
/// <c>current\Clicalo.exe</c> and there is no link anywhere in the path (<see cref="InstalledExecutable"/>): a copy
/// that was not installed has nothing to uninstall. Without a shell and without a command interpreter (LOG-008).
/// </summary>
public sealed class UninstallerLauncher
{
    /// <summary>The updater Velopack leaves in the root of the installation.</summary>
    public const string UpdaterName = "Update.exe";

    /// <summary>The argument that uninstalls.</summary>
    public const string UninstallArgument = "--uninstall";

    private readonly string? _installed;
    private readonly string? _running;
    private readonly Func<string, FileAttributes?> _attributes;
    private readonly Func<string, bool> _start;

    /// <summary>The uninstaller of this process.</summary>
    /// <param name="installedExecutable">The installed <c>Clicalo.exe</c>, or null for a copy that is not installed.</param>
    public UninstallerLauncher(string? installedExecutable)
        : this(installedExecutable, Environment.ProcessPath, InstalledExecutable.OnDisk, Run) { }

    /// <summary>The uninstaller over a fake disk and start (tests).</summary>
    internal UninstallerLauncher(
        string? installedExecutable,
        string? runningExecutable,
        Func<string, FileAttributes?> attributes,
        Func<string, bool> start
    )
    {
        ArgumentNullException.ThrowIfNull(attributes);
        ArgumentNullException.ThrowIfNull(start);
        _installed = installedExecutable;
        _running = runningExecutable;
        _attributes = attributes;
        _start = start;
    }

    /// <summary>Whether this copy was installed with the installer and its uninstaller is in place.</summary>
    public bool IsAvailable => Updater() is not null;

    /// <summary>Starts the uninstaller; false when this copy is not the installed one or Windows refused.</summary>
    public bool Start()
    {
        if (Updater() is not { } updater)
        {
            return false;
        }

        try
        {
            return _start(updater);
        }
        catch (Exception ex)
            when (ex
                    is System.ComponentModel.Win32Exception
                        or InvalidOperationException
                        or IOException
            )
        {
            return false;
        }
    }

    /// <summary>The verified <c>Update.exe</c> of the installation of this process, or null.</summary>
    private string? Updater()
    {
        if (!InstalledExecutable.IsVerified(_installed, _running, _attributes))
        {
            return null;
        }

        // <root>\current\Clicalo.exe → <root>\Update.exe
        var current = Path.GetDirectoryName(Path.GetFullPath(_installed!));
        var root = current is null ? null : Path.GetDirectoryName(current);
        if (string.IsNullOrEmpty(root))
        {
            return null;
        }

        var updater = Path.Combine(root, UpdaterName);
        return
            _attributes(updater) is { } file
            && !file.HasFlag(FileAttributes.Directory)
            && !file.HasFlag(FileAttributes.ReparsePoint)
            ? updater
            : null;
    }

    [SuppressMessage(
        "ApiDesign",
        "RS0030:Do not use banned APIs",
        Justification = "launch: starts the verified Update.exe of the installation directly, without a shell or a command interpreter (NFR-010, ADR-0029)."
    )]
    private static bool Run(string updater)
    {
        var start = new ProcessStartInfo(updater)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(updater) ?? string.Empty,
        };
        start.ArgumentList.Add(UninstallArgument);
        using var process = Process.Start(start);
        return process is not null;
    }
}
