using Clicalo.Domain.Privacy;
using Clicalo.Infrastructure.Logging;
using Clicalo.Infrastructure.Tests.Persistence;
using Clicalo.TestKit.Time;
using Serilog.Core;
using Serilog.Events;

namespace Clicalo.Infrastructure.Tests.Logging;

/// <summary>
/// The canary of blueprint §9.4 at the log layer: five <c>CANARY-&lt;guid&gt;</c> values as a shortcut text, a process, a
/// window title, a search and a key go through every way of logging them (plain, destructured, nested in a shortcut,
/// in an anonymous object, next to an exception); none reaches <c>clicalo.log</c>. The end-to-end canary over the whole
/// app, ETW and the diagnostic package runs in the CI (ADR-0008).
/// </summary>
[Trait("Req", "LOG-001")]
[Trait("Req", "LOG-003")]
[Trait("Req", "LOG-004")]
public sealed class LogCanaryTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void No_sensitive_value_reaches_the_log_file_whatever_the_template()
    {
        var canaries = Enumerable
            .Range(0, 5)
            .Select(_ => "CANARY-" + Guid.NewGuid().ToString("N"))
            .ToArray();
        var text = SecretText.From(canaries[0]);
        var process = new Sensitive<string>(canaries[1], RedactionKind.FreeText);
        var title = new Sensitive<string>(canaries[2], RedactionKind.WindowTitle);
        var search = new Sensitive<string>(canaries[3], RedactionKind.SearchQuery);
        var key = new Sensitive<string>(canaries[4], RedactionKind.Secret);

        using (
            var logger = ClicaloLog.Create(
                _folder.Locations,
                new LoggingLevelSwitch(LogEventLevel.Verbose)
            )
        )
        {
            logger.Information(
                "typing {Text} in {Process} titled {Title} searching {Search} with {Key}",
                text,
                process,
                title,
                search,
                key
            );
            logger.Information(
                "destructured {@Text} {@Process} {@Title} {@Search} {@Key}",
                text,
                process,
                title,
                search,
                key
            );
            logger.Information(
                "stringified {$Text} {$Process} {$Title} {$Search} {$Key}",
                text,
                process,
                title,
                search,
                key
            );
            logger.Information("nested {@Shortcut}", TestDocuments.Text("sig", canaries[0]));
            logger.Information(
                "anonymous {@Value}",
                new
                {
                    Title = title,
                    Key = key,
                    Texts = new[] { text },
                }
            );
            logger.Information(
                "dictionary {@Map}",
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["title"] = title,
                    ["search"] = search,
                }
            );
            logger.Error(
                new InvalidOperationException("engine fault"),
                "fault next to {Process}",
                process
            );
        }

        var written = string.Concat(
            Directory.GetFiles(_folder.Locations.Logs).Select(File.ReadAllText)
        );
        written.ShouldNotBeEmpty();
        foreach (var canary in canaries)
        {
            written.ShouldNotContain(canary);
        }

        written.ShouldContain("[hidden · " + canaries[0].Length + " chars]");
        written.ShouldContain("[redacted WindowTitle]");
    }

    [Fact]
    public void A_full_address_keeps_only_its_host()
    {
        var canary = "CANARY-" + Guid.NewGuid().ToString("N");

        using (var logger = ClicaloLog.Create(_folder.Locations, ClicaloLog.DefaultLevel()))
        {
            logger.Information(
                "open {Url}",
                new Uri("https://mail.example.org/" + canary + "?q=" + canary)
            );
            logger.Information(
                "open {@Shortcut}",
                TestDocuments.Url("web", "https://mail.example.org/" + canary)
            );
        }

        var written = File.ReadAllText(_folder.Locations.LogFile);
        written.ShouldNotContain(canary);
        written.ShouldContain("https://mail.example.org/…");
    }

    [Fact]
    public void Each_line_carries_its_sequence_thread_and_event_code()
    {
        using (var logger = ClicaloLog.Create(_folder.Locations, ClicaloLog.DefaultLevel()))
        {
            logger.Information("one");
            logger.Information("two");
        }

        var lines = File.ReadAllLines(_folder.Locations.LogFile);
        lines.Length.ShouldBe(2);
        lines[0]
            .ShouldMatch(
                @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}[+-]\d{2}:\d{2} \[INF\] #1 .+ - one$"
            );
        lines[1].ShouldContain("#2 ");
    }

    [Fact]
    public void Debug_turns_itself_off_after_its_window()
    {
        var level = ClicaloLog.DefaultLevel();
        var time = TestTime.CreateProvider();
        using var window = new DebugLoggingWindow(level, time);

        window.Open();
        window.IsOpen.ShouldBeTrue();
        time.Advance(TimeSpan.FromMinutes(59));
        window.IsOpen.ShouldBeTrue();
        time.Advance(TimeSpan.FromMinutes(1));

        window.IsOpen.ShouldBeFalse();
        level.MinimumLevel.ShouldBe(LogEventLevel.Information);
    }
}
