using System.IO;

namespace Clicalo.App.Tests;

/// <summary>
/// The command line of <c>Clicalo.exe</c>: a normal start has no option, Sentinel's relaunch adds the crash and the
/// safe mode (ADR-0018), and development and measurement options never change what a normal start does.
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
            [.. Platform.Core.Guardian.CrashJournal.RelaunchArguments(crash, safeMode: true)],
            Default
        );

        options.AfterCrash.ShouldBe(crash);
        options.SafeMode.ShouldBeTrue();
        options.SendInput.ShouldBeTrue("a relaunch after a crash still sends keys");
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
}
