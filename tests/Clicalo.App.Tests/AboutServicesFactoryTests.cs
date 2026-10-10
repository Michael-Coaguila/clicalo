using System.IO;
using System.Text;
using Clicalo.App.Composition;

namespace Clicalo.App.Tests;

/// <summary>
/// What «Acerca de y contacto» reads from the computer (ACE-003, ACE-004): the newest part of <c>clicalo.log</c> while
/// the log sink holds it open, and the real Windows version.
/// </summary>
public sealed class AboutServicesFactoryTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "clicalo-app-tests",
        Guid.NewGuid().ToString("N")
    );

    public AboutServicesFactoryTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
            // Cleaned by the system.
        }
    }

    [Fact]
    [Trait("Req", "ACE-003")]
    public async Task The_log_is_read_while_the_sink_keeps_it_open()
    {
        var path = Path.Combine(_folder, "clicalo.log");
        await using var sink = new FileStream(
            path,
            new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.Read | FileShare.Delete,
            }
        );
        var lines = Encoding.UTF8.GetBytes(
            "INF app.started\nINF text.sent [oculto · 23 caracteres]\n"
        );
        await sink.WriteAsync(lines, TestContext.Current.CancellationToken);
        await sink.FlushAsync(TestContext.Current.CancellationToken);

        var log = await AboutServicesFactory.ReadLogAsync(
            path,
            TestContext.Current.CancellationToken
        );

        log.ShouldBe("INF app.started\nINF text.sent [oculto · 23 caracteres]");
    }

    [Fact]
    [Trait("Req", "ACE-003")]
    public async Task A_long_log_shows_its_newest_part_from_a_whole_line()
    {
        var path = Path.Combine(_folder, "clicalo.log");
        var text = new StringBuilder();
        for (var i = 0; i < 4000; i++)
        {
            text.Append("INF line ").Append(i).Append(" of the log\n");
        }

        await File.WriteAllTextAsync(path, text.ToString(), TestContext.Current.CancellationToken);

        var log = (
            await AboutServicesFactory.ReadLogAsync(path, TestContext.Current.CancellationToken)
        )!;

        log.Length.ShouldBeLessThanOrEqualTo(64 * 1024);
        log.ShouldStartWith("INF line ");
        log.ShouldEndWith("INF line 3999 of the log");
    }

    [Fact]
    [Trait("Req", "ACE-003")]
    public async Task Without_a_log_there_is_nothing_to_show()
    {
        var log = await AboutServicesFactory.ReadLogAsync(
            Path.Combine(_folder, "missing.log"),
            TestContext.Current.CancellationToken
        );

        log.ShouldBe(string.Empty);
    }

    [Fact]
    [Trait("Req", "ACE-004")]
    public void The_windows_version_names_the_release_and_the_build()
    {
        AboutServicesFactory.WindowsVersion().ShouldMatch(@"^Windows 1[01] \(\d+\)$");
    }

    [Fact]
    [Trait("Req", "ACE-004")]
    [Trait("Req", "LOG-008")]
    public async Task An_email_address_only_opens_through_its_own_entry()
    {
        // ADR-0029: the Shell thread only starts http and https; a mailto: address goes through the entry that checks
        // it is for the project, and without that entry (--no-input) nothing opens.
        var shell = new Clicalo.App.Composition.DeferredShellExecutor();
        var mail = new Uri("mailto:contacto@clicalo.example?subject=a");
        var asked = new List<Uri>();

        var withEntry = await AboutServicesFactory.OpenAsync(
            shell,
            (address, _) =>
            {
                asked.Add(address);
                return Task.FromResult(true);
            },
            mail,
            TestContext.Current.CancellationToken
        );
        var withoutEntry = await AboutServicesFactory.OpenAsync(
            shell,
            null,
            mail,
            TestContext.Current.CancellationToken
        );

        withEntry.ShouldBeTrue();
        asked.ShouldBe([mail]);
        withoutEntry.ShouldBeFalse();
    }
}
