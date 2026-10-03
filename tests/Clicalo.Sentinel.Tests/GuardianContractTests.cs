using Clicalo.Platform.Core.Guardian;
using Clicalo.Platform.Core.KeyLedger;

namespace Clicalo.Sentinel.Tests;

/// <summary>
/// The start-up contract of Sentinel (ADR-0018), the relaunch policy (blueprint §3.1, S9) and the crash journal it
/// reads.
/// </summary>
[Trait("Req", "SEG-006")]
public sealed class GuardianContractTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 10, 0, 0, TimeSpan.Zero);

    private static readonly SentinelStartInfo Info = new(
        0x1A4,
        0x2B8,
        0x3CC,
        TimeSpan.FromSeconds(1),
        3,
        TimeSpan.FromMinutes(10),
        TimeSpan.FromSeconds(30)
    );

    [Fact]
    public void The_arguments_are_the_ones_of_the_ADR_in_order()
    {
        Info.ToArguments()
            .ShouldBe([
                "--protocol=2",
                "--parent=0x1A4",
                "--ledger=0x2B8",
                "--pipe=0x3CC",
                "--heartbeat-ms=1000",
                "--crash-loop=3/600000",
                "--refused-release-wait-ms=30000",
            ]);
        SentinelStartInfo.ProtocolVersion.ShouldBe(2);
        Info.InheritedHandles.Length.ShouldBe(SentinelStartInfo.InheritedHandleCount);
    }

    [Fact]
    public void The_arguments_round_trip()
    {
        SentinelStartInfo.TryParse(Info.ToArguments().AsSpan(), out var parsed).ShouldBeTrue();

        parsed.ShouldBe(Info);
    }

    [Theory]
    [InlineData("--protocol=1")]
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
    [InlineData(2, "--ledger=12")]
    [InlineData(3, "--pipe=0xZZ")]
    [InlineData(4, "--heartbeat-ms=0")]
    [InlineData(5, "--crash-loop=3")]
    [InlineData(5, "--crash-loop=0/1000")]
    [InlineData(6, "--refused-release-wait-ms=0")]
    [InlineData(6, "--refused-release-wait-ms=-5")]
    [InlineData(6, "--refused-release-wait-ms=")]
    [InlineData(6, "--crash-loop=3/600000")]
    [InlineData(0, "--parent=0x1A4")]
    public void A_malformed_or_misplaced_argument_is_refused(int index, string argument)
    {
        var arguments = Info.ToArguments().SetItem(index, argument);

        SentinelStartInfo.TryParse(arguments.AsSpan(), out _).ShouldBeFalse();
    }

    [Fact]
    public void Missing_or_extra_arguments_are_refused()
    {
        SentinelStartInfo.TryParse(Info.ToArguments().RemoveAt(5).AsSpan(), out _).ShouldBeFalse();
        SentinelStartInfo.TryParse(Info.ToArguments().RemoveAt(6).AsSpan(), out _).ShouldBeFalse();
        SentinelStartInfo.TryParse(Info.ToArguments().Add("--x").AsSpan(), out _).ShouldBeFalse();
        SentinelStartInfo.TryParse([], out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData(LedgerMarks.CleanShutdown)]
    [InlineData(LedgerMarks.NoRelaunch)]
    [InlineData(LedgerMarks.CleanShutdown | LedgerMarks.NoRelaunch)]
    [Trait("Req", "SIS-004")]
    public void A_clean_exit_an_update_or_a_handover_is_never_relaunched(LedgerMarks marks) =>
        RelaunchPolicy
            .Decide(marks, [], Now, 3, TimeSpan.FromMinutes(10))
            .ShouldBe(RelaunchDecision.None);

    [Fact]
    [Trait("Req", "SIS-004")]
    public void A_crash_is_relaunched_and_the_third_in_ten_minutes_enters_safe_mode()
    {
        var window = TimeSpan.FromMinutes(10);

        RelaunchPolicy
            .Decide(LedgerMarks.EngineAlive, [], Now, 3, window)
            .ShouldBe(RelaunchDecision.Relaunch);
        RelaunchPolicy
            .Decide(LedgerMarks.None, [Now.AddMinutes(-9)], Now, 3, window)
            .ShouldBe(RelaunchDecision.Relaunch);
        RelaunchPolicy
            .Decide(LedgerMarks.None, [Now.AddMinutes(-9), Now.AddMinutes(-1)], Now, 3, window)
            .ShouldBe(RelaunchDecision.RelaunchInSafeMode);
        RelaunchPolicy
            .Decide(LedgerMarks.None, [Now.AddMinutes(-11), Now.AddMinutes(-1)], Now, 3, window)
            .ShouldBe(RelaunchDecision.Relaunch);
    }

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
        CrashJournal
            .RelaunchArguments(Now, safeMode: false)
            .ShouldBe(["--after-crash=" + Now.ToUnixTimeMilliseconds()]);
        CrashJournal
            .RelaunchArguments(Now, safeMode: true)
            .Contains(CrashJournal.SafeModeArgument, StringComparer.Ordinal)
            .ShouldBeTrue();
    }
}
