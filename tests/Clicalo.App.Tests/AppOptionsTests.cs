using System.IO;
using Clicalo.App.Composition;
using Clicalo.App.SingleInstance;
using Clicalo.Platform.Core.Guardian;
using Clicalo.TestKit;

namespace Clicalo.App.Tests;

/// <summary>
/// The command line of <c>Clicalo.exe</c>: a normal start has no option, Sentinel's relaunch adds the crash and the
/// safe mode (ADR-0018) and keeps the folder of <c>--data</c> (ADR-0032), and development and measurement options
/// never change what a normal start does.
/// </summary>
public sealed class AppOptionsTests
{
    private const string Default = @"C:\Users\someone\AppData\Roaming\Clicalo";

    [Fact]
    public void A_normal_start_sends_keys_with_the_user_data_and_nothing_else()
    {
        var options = AppOptions.Parse([], Default);

        options.ShouldBe(new AppOptions(Default, SendInput: true, GuardianAfterFirstFrame: false));
        options.IsolatedData.ShouldBeFalse();
        options.AfterCrash.ShouldBeNull();
        options.SafeMode.ShouldBeFalse();
        options.ExitAfter.ShouldBeNull();
    }

    [Fact]
    [Trait("Req", "SEG-006")]
    public void No_input_and_an_isolated_folder_are_what_cl_run_passes()
    {
        var folder = Path.Combine(Path.GetTempPath(), "clicalo-dev");

        var options = AppOptions.Parse(["--no-input", "--data", folder], Default);

        options.SendInput.ShouldBeFalse();
        options.DataDirectory.ShouldBe(Path.GetFullPath(folder));
        options.IsolatedData.ShouldBeTrue();
    }

    [Fact]
    public void Sentinel_relaunch_arguments_carry_the_crash_and_the_safe_mode()
    {
        var crash = new DateTimeOffset(2026, 9, 26, 10, 0, 0, TimeSpan.Zero);

        var options = AppOptions.Parse(
            [.. CrashJournal.RelaunchArguments(crash, safeMode: true, dataFolder: null)],
            Default
        );

        options.AfterCrash.ShouldBe(crash);
        options.SafeMode.ShouldBeTrue();
        options.SendInput.ShouldBeTrue("a relaunch after a crash still sends keys");
        options.DataDirectory.ShouldBe(Default);
        options.IsolatedData.ShouldBeFalse();
    }

    [Theory]
    [Trait("Req", "SIS-004")]
    [Trait("Req", "SIS-003")]
    [Trait("Req", "REG-08")]
    [InlineData(@"C:\Users\Ana\Mis datos\Clícalo")]
    [InlineData(@"C:\Users\Ana\Mis datos\Clícalo\")]
    [InlineData(@"\\servidor\compartida\datos de José")]
    [InlineData(@"D:\año 2026\")]
    public void An_isolated_copy_relaunched_by_Sentinel_comes_back_to_its_folder(string folder)
    {
        var crash = new DateTimeOffset(2026, 10, 22, 10, 0, 0, TimeSpan.Zero);
        var original = AppOptions.Parse(["--data", folder], Default);

        // What Sentinel writes (GuardianProcess.Start) and Windows splits for the relaunched Clicalo.exe.
        var relaunched = AppOptions.Parse(
            Relaunched(
                CommandLineArguments.Join(
                    CrashJournal.RelaunchArguments(crash, safeMode: true, original.DataDirectory)
                )
            ),
            Default
        );

        relaunched.DataDirectory.ShouldBe(original.DataDirectory);
        relaunched.IsolatedData.ShouldBeTrue();
        relaunched.AfterCrash.ShouldBe(crash);
        relaunched.SafeMode.ShouldBeTrue();
        InstanceIdentity
            .Current(relaunched)
            .MutexName.ShouldBe(InstanceIdentity.Current(original).MutexName);
        AppDataLocations
            .CrashJournal(AppDataLocations.For(relaunched))
            .ShouldBe(Path.Combine(original.DataDirectory, CrashJournal.FileName));
    }

    [Theory]
    [InlineData("--after-crash=")]
    [InlineData("--after-crash=-5")]
    [InlineData("--after-crash=12ab")]
    [InlineData("--after-crash=99999999999999999999")]
    public void A_malformed_crash_time_is_ignored(string argument) =>
        AppOptions.Parse([argument], Default).AfterCrash.ShouldBeNull();

    [Fact]
    public void The_diagnostic_exit_and_the_guardian_moment_are_read_with_their_values()
    {
        var options = AppOptions.Parse(
            ["--exit-after", "3", "--guardian", "after-first-frame"],
            Default
        );

        options.ExitAfter.ShouldBe(TimeSpan.FromSeconds(3));
        options.GuardianAfterFirstFrame.ShouldBeTrue();
    }

    [Theory]
    [InlineData("--exit-after")]
    [InlineData("--exit-after", "soon")]
    [InlineData("--exit-after", "-1")]
    [InlineData("--data")]
    [InlineData("--unknown", "--word")]
    public void Unknown_or_incomplete_options_are_ignored(params string[] arguments)
    {
        var options = AppOptions.Parse(arguments, Default);

        options.ExitAfter.ShouldBeNull();
        options.DataDirectory.ShouldBe(Default);
        options.SendInput.ShouldBeTrue();
    }

    /// <summary>The arguments a <c>Clicalo.exe</c> started with <paramref name="arguments"/> receives.</summary>
    private static string[] Relaunched(string arguments) =>
        [
            .. WindowsCommandLine
                .Split("\"C:\\Program Files\\Clícalo\\Clicalo.exe\" " + arguments)
                .Skip(1),
        ];
}
