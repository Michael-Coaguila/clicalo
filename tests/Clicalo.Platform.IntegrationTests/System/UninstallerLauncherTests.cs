using Clicalo.Platform.Windows.Launch;

namespace Clicalo.Platform.IntegrationTests.SystemSection;

/// <summary>
/// «Desinstalar Clícalo» over a fake disk and a fake start (NFR-010, proposal P6, ADR-0029): only the installed copy,
/// exactly the running one and without links on its path, ever starts its own <c>Update.exe</c>. Nothing is started
/// for real.
/// </summary>
[Trait("Req", "NFR-010")]
[Trait("Req", "LOG-008")]
public sealed class UninstallerLauncherTests
{
    private const string Root = @"C:\Users\Ana\AppData\Local\Clicalo.App";
    private const string Installed = Root + @"\current\Clicalo.exe";
    private const string Updater = Root + @"\Update.exe";

    private readonly List<string> _started = [];
    private readonly Dictionary<string, FileAttributes> _disk = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        [@"C:\"] = FileAttributes.Directory,
        [@"C:\Users"] = FileAttributes.Directory,
        [@"C:\Users\Ana"] = FileAttributes.Directory,
        [@"C:\Users\Ana\AppData"] = FileAttributes.Directory | FileAttributes.Hidden,
        [@"C:\Users\Ana\AppData\Local"] = FileAttributes.Directory,
        [Root] = FileAttributes.Directory,
        [Root + @"\current"] = FileAttributes.Directory,
        [Installed] = FileAttributes.Archive,
        [Updater] = FileAttributes.Archive,
    };

    [Fact]
    public void The_installed_copy_starts_the_updater_of_its_own_installation()
    {
        var launcher = Launcher(Installed, Installed);

        launcher.IsAvailable.ShouldBeTrue();
        launcher.Start().ShouldBeTrue();

        _started.ShouldBe([Updater]);
        UninstallerLauncher.UninstallArgument.ShouldBe("--uninstall");
    }

    [Fact]
    public void A_copy_that_was_not_installed_has_nothing_to_uninstall()
    {
        _disk[@"C:\Temp\Clicalo.exe"] = FileAttributes.Archive;

        Launcher(null, Installed).IsAvailable.ShouldBeFalse();
        Launcher(Installed, @"C:\Temp\Clicalo.exe").IsAvailable.ShouldBeFalse();
        Launcher(Installed, @"C:\Temp\Clicalo.exe").Start().ShouldBeFalse();
        _started.ShouldBeEmpty();
    }

    [Fact]
    public void A_missing_or_linked_updater_is_never_started()
    {
        _ = _disk.Remove(Updater);
        Launcher(Installed, Installed).IsAvailable.ShouldBeFalse();

        _disk[Updater] = FileAttributes.Archive | FileAttributes.ReparsePoint;
        Launcher(Installed, Installed).Start().ShouldBeFalse();

        _disk[Updater] = FileAttributes.Archive;
        _disk[Root] = FileAttributes.Directory | FileAttributes.ReparsePoint;
        Launcher(Installed, Installed).Start().ShouldBeFalse();
        _started.ShouldBeEmpty();
    }

    [Fact]
    public void A_start_Windows_refuses_is_reported_and_never_thrown()
    {
        var launcher = new UninstallerLauncher(
            Installed,
            Installed,
            Attributes,
            _ => throw new System.ComponentModel.Win32Exception(5)
        );

        launcher.Start().ShouldBeFalse();
    }

    private UninstallerLauncher Launcher(string? installed, string running) =>
        new(
            installed,
            running,
            Attributes,
            updater =>
            {
                _started.Add(updater);
                return true;
            }
        );

    private FileAttributes? Attributes(string path) =>
        _disk.TryGetValue(path, out var attributes) ? attributes : null;
}
