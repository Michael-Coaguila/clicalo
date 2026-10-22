using Clicalo.Platform.Core.Guardian;
using Clicalo.TestKit;

namespace Clicalo.Sentinel.Tests;

/// <summary>
/// The start-up contract of Sentinel (ADR-0023, protocol 4 of ADR-0032), the relaunch policy (blueprint §3.1, S9) and
/// the crash journal it reads.
/// </summary>
[Trait("Req", "SEG-006")]
public sealed class GuardianContractTests
{
    private const string Folder = @"C:\Users\Ana\Mis datos\Clícalo\";

    private static readonly DateTimeOffset Now = new(2026, 9, 26, 10, 0, 0, TimeSpan.Zero);

    private static readonly SentinelStartInfo Info = new(
        0x1A4,
        TimeSpan.FromSeconds(1),
        3,
        TimeSpan.FromMinutes(10)
    );

    [Fact]
    public void The_arguments_are_the_ones_of_the_ADR_in_order()
    {
        Info.ToArguments()
            .ShouldBe([
                "--protocol=4",
                "--parent=0x1A4",
                "--retry-ms=1000",
                "--crash-loop=3/600000",
            ]);
        SentinelStartInfo.ProtocolVersion.ShouldBe(4);
        Info.InheritedHandles.ShouldBe([(nint)0x1A4]);
        Info.DataFolder.ShouldBeNull();
    }

    [Fact]
    [Trait("Req", "SIS-004")]
    [Trait("Req", "REG-08")]
    public void An_isolated_copy_adds_its_data_folder_last()
    {
        var isolated = Info with { DataFolder = Folder };

        isolated.ToArguments().ShouldBe([.. Info.ToArguments(), "--data=" + Folder]);
        isolated.InheritedHandles.ShouldBe(Info.InheritedHandles);
    }

    [Fact]
    public void The_arguments_round_trip()
    {
        SentinelStartInfo.TryParse(Info.ToArguments().AsSpan(), out var parsed).ShouldBeTrue();

        parsed.ShouldBe(Info);
    }

    [Theory]
    [Trait("Req", "SIS-004")]
    [Trait("Req", "REG-08")]
    [InlineData(null)]
    [InlineData(Folder)]
    [InlineData(@"C:\Users\Ana\Mis datos\Clícalo")]
    [InlineData(@"\\servidor\compartida\datos de José")]
    [InlineData(@"D:\")]
    public void The_command_line_round_trips_through_Windows(string? folder)
    {
        const string Sentinel = @"C:\Program Files\Clícalo\Clicalo.Sentinel.exe";
        var info = Info with { DataFolder = folder };

        var split = WindowsCommandLine.Split(
            '"' + Sentinel + "\" " + CommandLineArguments.Join(info.ToArguments())
        );

        split[0].ShouldBe(Sentinel);
        SentinelStartInfo.TryParse(split.Skip(1).ToArray(), out var parsed).ShouldBeTrue();
        parsed.ShouldBe(info);
    }

    [Theory]
    [InlineData("--protocol=1")]
    [InlineData("--protocol=2")]
    [InlineData("--protocol=3")]
    [InlineData("--protocol=x")]
    [InlineData("--protocol=")]
    public void Another_protocol_version_is_refused(string protocol)
    {
        var arguments = Info.ToArguments().SetItem(0, protocol);

        SentinelStartInfo.TryParse(arguments.AsSpan(), out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData(1, "--parent=0x0")]
    [InlineData(1, "--parent=12")]
    [InlineData(1, "--parent=0xZZ")]
    [InlineData(2, "--retry-ms=0")]
    [InlineData(2, "--retry-ms=-5")]
    [InlineData(2, "--retry-ms=")]
    [InlineData(3, "--crash-loop=3")]
    [InlineData(3, "--crash-loop=0/1000")]
    [InlineData(3, "--crash-loop=3/0")]
    [InlineData(3, "--retry-ms=1000")]
    [InlineData(0, "--parent=0x1A4")]
    public void A_malformed_argument_is_refused(int index, string argument)
    {
        var arguments = Info.ToArguments().SetItem(index, argument);

        SentinelStartInfo.TryParse(arguments.AsSpan(), out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData("--data=")]
    [InlineData(@"--data=datos\de prueba")]
    [InlineData("--data=C:datos")]
    [InlineData(@"--data=\datos")]
    [InlineData("--data=C:\\a\"b")]
    [InlineData("--data=C:\\a\tb")]
    [InlineData(@"--folder=C:\datos")]
    [InlineData("--x")]
    public void A_malformed_data_folder_is_refused(string argument) =>
        SentinelStartInfo
            .TryParse(Info.ToArguments().Add(argument).AsSpan(), out _)
            .ShouldBeFalse();

    [Fact]
    public void Missing_or_extra_arguments_are_refused()
    {
        var isolated = (Info with { DataFolder = Folder }).ToArguments();

        SentinelStartInfo.TryParse(Info.ToArguments().RemoveAt(3).AsSpan(), out _).ShouldBeFalse();
        SentinelStartInfo.TryParse(isolated.Add("--x").AsSpan(), out _).ShouldBeFalse();
        SentinelStartInfo.TryParse(isolated.RemoveAt(0).AsSpan(), out _).ShouldBeFalse();
        SentinelStartInfo.TryParse([], out _).ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "SIS-004")]
    public void An_exit_code_of_zero_is_never_relaunched() =>
        RelaunchPolicy
            .Decide(0, [Now.AddMinutes(-1), Now.AddMinutes(-2)], Now, 3, TimeSpan.FromMinutes(10))
            .ShouldBe(RelaunchDecision.None);

    [Fact]
    [Trait("Req", "SIS-004")]
    public void A_crash_is_relaunched_and_the_third_in_ten_minutes_enters_safe_mode()
    {
        var window = TimeSpan.FromMinutes(10);

        RelaunchPolicy.Decide(1, [], Now, 3, window).ShouldBe(RelaunchDecision.Relaunch);
        RelaunchPolicy
            .Decide(-1, [Now.AddMinutes(-9)], Now, 3, window)
            .ShouldBe(RelaunchDecision.Relaunch);
        RelaunchPolicy
            .Decide(-1, [Now.AddMinutes(-9), Now.AddMinutes(-1)], Now, 3, window)
            .ShouldBe(RelaunchDecision.RelaunchInSafeMode);
        RelaunchPolicy
            .Decide(-1, [Now.AddMinutes(-11), Now.AddMinutes(-1)], Now, 3, window)
            .ShouldBe(RelaunchDecision.Relaunch);
    }

    [Fact]
    [Trait("Req", "SIS-004")]
    public void A_crash_after_the_safe_mode_relaunch_inside_the_window_stops_the_loop() =>
        RelaunchPolicy
            .Decide(
                -1,
                [Now.AddMinutes(-9), Now.AddMinutes(-5), Now.AddMinutes(-1)],
                Now,
                3,
                TimeSpan.FromMinutes(10)
            )
            .ShouldBe(RelaunchDecision.None);

    [Fact]
    public void The_crash_journal_round_trips_and_keeps_the_newest_entries()
    {
        var crashes = Enumerable
            .Range(0, CrashJournal.MaxEntries + 5)
            .Select(i => Now.AddMinutes(-i))
            .ToList();

        var json = CrashJournal.Append(crashes.Skip(1), crashes[0]);
        var parsed = CrashJournal.Parse(json);

        parsed.Length.ShouldBe(CrashJournal.MaxEntries);
        parsed.ShouldContain(Now);
        parsed.ShouldBe(parsed.Order().ToArray());
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("{\"format\":\"other\",\"crashes\":[\"2026-09-26T10:00:00+00:00\"]}")]
    public void A_journal_that_cannot_be_read_counts_as_empty(string text) =>
        CrashJournal.Parse(System.Text.Encoding.UTF8.GetBytes(text)).ShouldBeEmpty();

    [Fact]
    public void The_relaunch_arguments_say_when_it_crashed_and_whether_to_enter_safe_mode()
    {
        var after = "--after-crash=" + Now.ToUnixTimeMilliseconds();

        CrashJournal.RelaunchArguments(Now, safeMode: false, dataFolder: null).ShouldBe([after]);
        CrashJournal
            .RelaunchArguments(Now, safeMode: true, dataFolder: null)
            .ShouldBe([after, "--safe-mode"]);
    }

    [Fact]
    [Trait("Req", "SIS-004")]
    [Trait("Req", "REG-08")]
    public void The_relaunch_of_an_isolated_copy_keeps_its_data_folder()
    {
        var after = "--after-crash=" + Now.ToUnixTimeMilliseconds();

        CrashJournal
            .RelaunchArguments(Now, safeMode: false, Folder)
            .ShouldBe([after, "--data", Folder]);
        CrashJournal
            .RelaunchArguments(Now, safeMode: true, Folder)
            .ShouldBe([after, "--safe-mode", "--data", Folder]);
    }

    [Theory]
    [InlineData(Folder, true)]
    [InlineData(@"\\servidor\compartida\datos", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData(@"datos\de prueba", false)]
    [InlineData("C:datos", false)]
    [InlineData("C:\\a\"b", false)]
    public void Only_a_full_path_without_quotes_is_a_data_folder(string? folder, bool expected) =>
        CrashJournal.IsDataFolder(folder).ShouldBe(expected);
}
