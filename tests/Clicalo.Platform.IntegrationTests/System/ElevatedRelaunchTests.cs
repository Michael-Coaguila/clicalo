using Clicalo.Application.Ports;
using Clicalo.Platform.Windows.Elevation;

namespace Clicalo.Platform.IntegrationTests.SystemSection;

/// <summary>
/// «Reabrir como administrador» over a fake launcher and a fake disk (user decision D7, LOG-007, ADR-0027): only the
/// installed executable, exactly the running one and without links on its path, ever reaches the UAC prompt, with
/// <c>--handover=&lt;pid&gt;</c>; a cancelled prompt and a failure are told apart. Nothing is started for real.
/// </summary>
[Trait("Req", "SIS-002")]
[Trait("Req", "LOG-007")]
public sealed class ElevatedRelaunchTests
{
    private const string Installed = @"C:\Users\Ana\AppData\Local\Clicalo.App\current\Clicalo.exe";

    private readonly FakeLauncher _launcher = new();
    private readonly Dictionary<string, FileAttributes> _disk = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        [@"C:\"] = FileAttributes.Directory,
        [@"C:\Users"] = FileAttributes.Directory,
        [@"C:\Users\Ana"] = FileAttributes.Directory,
        [@"C:\Users\Ana\AppData"] = FileAttributes.Directory | FileAttributes.Hidden,
        [@"C:\Users\Ana\AppData\Local"] = FileAttributes.Directory,
        [@"C:\Users\Ana\AppData\Local\Clicalo.App"] = FileAttributes.Directory,
        [@"C:\Users\Ana\AppData\Local\Clicalo.App\current"] = FileAttributes.Directory,
        [Installed] = FileAttributes.Archive,
    };

    [Fact]
    public async Task The_installed_copy_asks_Windows_with_the_handover()
    {
        var outcome = await Relaunch(Installed).RelaunchAsync(CancellationToken.None);

        outcome.ShouldBe(ElevationOutcome.Started);
        _launcher.Calls.ShouldBe([(Installed, "--handover=4242")]);
    }

    [Theory]
    [InlineData(ElevationOutcome.Cancelled)]
    [InlineData(ElevationOutcome.Failed)]
    public async Task What_Windows_answers_is_reported(ElevationOutcome answer)
    {
        _launcher.Answer = answer;

        (await Relaunch(Installed).RelaunchAsync(CancellationToken.None)).ShouldBe(answer);
    }

    [Fact]
    public async Task Another_copy_never_reaches_the_prompt()
    {
        _disk[@"C:\Temp\Clicalo.exe"] = FileAttributes.Archive;

        (await Relaunch(@"C:\Temp\Clicalo.exe").RelaunchAsync(CancellationToken.None)).ShouldBe(
            ElevationOutcome.NotInstalled
        );
        (
            await new ElevatedRelaunch(
                null,
                Installed,
                4242,
                false,
                _launcher,
                Attributes
            ).RelaunchAsync(CancellationToken.None)
        ).ShouldBe(ElevationOutcome.NotInstalled);
        _launcher.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_link_on_the_path_never_reaches_the_prompt()
    {
        _disk[@"C:\Users\Ana\AppData\Local\Clicalo.App\current"] =
            FileAttributes.Directory | FileAttributes.ReparsePoint;

        (await Relaunch(Installed).RelaunchAsync(CancellationToken.None)).ShouldBe(
            ElevationOutcome.NotInstalled
        );
        _launcher.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_missing_executable_never_reaches_the_prompt()
    {
        _ = _disk.Remove(Installed);

        (await Relaunch(Installed).RelaunchAsync(CancellationToken.None)).ShouldBe(
            ElevationOutcome.NotInstalled
        );
        _launcher.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_elevated_instance_does_not_reopen()
    {
        var relaunch = new ElevatedRelaunch(
            Installed,
            Installed,
            4242,
            true,
            _launcher,
            Attributes
        );

        relaunch.IsElevated.ShouldBeTrue();
        (await relaunch.RelaunchAsync(CancellationToken.None)).ShouldBe(
            ElevationOutcome.NotInstalled
        );
        _launcher.Calls.ShouldBeEmpty();
    }

    [Fact]
    public void The_handover_names_the_process_to_wait_for() =>
        ElevatedRelaunch.HandoverArguments(17).ShouldBe("--handover=17");

    private ElevatedRelaunch Relaunch(string running) =>
        new(Installed, running, 4242, false, _launcher, Attributes);

    private FileAttributes? Attributes(string path) =>
        _disk.TryGetValue(path, out var attributes) ? attributes : null;

    private sealed class FakeLauncher : IElevationLauncher
    {
        public ElevationOutcome Answer { get; set; } = ElevationOutcome.Started;

        public List<(string Executable, string Arguments)> Calls { get; } = [];

        public ElevationOutcome Launch(string executable, string arguments)
        {
            lock (Calls)
            {
                Calls.Add((executable, arguments));
            }

            return Answer;
        }
    }
}
