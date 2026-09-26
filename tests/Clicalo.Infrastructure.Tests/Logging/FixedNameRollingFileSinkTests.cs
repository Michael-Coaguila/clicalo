using System.Globalization;
using Clicalo.Infrastructure.Logging;
using Clicalo.Infrastructure.Tests.Persistence;
using Serilog;
using Serilog.Formatting.Display;

namespace Clicalo.Infrastructure.Tests.Logging;

/// <summary>The log file keeps a fixed name and rotates at a size (blueprint §9.4, LOG-001).</summary>
[Trait("Req", "LOG-001")]
public sealed class FixedNameRollingFileSinkTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    private string LogFile => _folder.Locations.LogFile;

    [Fact]
    public void Writes_to_clicalo_log()
    {
        using (var logger = Logger(maxBytes: 1_000_000, files: 5))
        {
            logger.Information("first {Value}", 1);
        }

        File.ReadAllText(LogFile).ShouldContain("first 1");
        _folder.Files().ShouldBe(["roaming/logs/clicalo.log"]);
    }

    [Fact]
    public void Rotates_at_the_size_keeping_the_fixed_name_and_at_most_five_files()
    {
        using (var logger = Logger(maxBytes: 300, files: 5))
        {
            for (var i = 0; i < 80; i++)
            {
                logger.Information(
                    "line {Number} with some padding text",
                    i.ToString("D3", CultureInfo.InvariantCulture)
                );
            }
        }

        _folder
            .Files()
            .ShouldBe([
                "roaming/logs/clicalo.1.log",
                "roaming/logs/clicalo.2.log",
                "roaming/logs/clicalo.3.log",
                "roaming/logs/clicalo.4.log",
                "roaming/logs/clicalo.log",
            ]);
        File.ReadAllText(LogFile).ShouldContain("line 079");
        File.ReadAllText(FixedNameRollingFileSink.RotationOf(LogFile, 1))
            .ShouldNotContain("line 079");
        _folder
            .Files()
            .Select(f => File.ReadAllText(_folder.PathOf(f)))
            .ShouldNotContain(t => t.Contains("line 000", StringComparison.Ordinal));
        foreach (var file in _folder.Files())
        {
            new FileInfo(_folder.PathOf(file)).Length.ShouldBeLessThanOrEqualTo(300);
        }
    }

    [Fact]
    public void Removes_the_windows_user_when_writing_even_from_exceptions()
    {
        var redactor = new UserPathRedactor(@"C:\Users\Ana María", "Ana María");
        using (var logger = Logger(maxBytes: 1_000_000, files: 5, redactor))
        {
            logger.Warning(
                new IOException(@"Could not open C:\Users\Ana María\Documents\x.json"),
                "failed on {Path}",
                @"D:\Perfiles\Ana María\data.json"
            );
        }

        var text = File.ReadAllText(LogFile);
        text.ShouldNotContain("Ana María");
        text.ShouldContain(@"%USERPROFILE%\Documents\x.json");
        text.ShouldContain(@"D:\Perfiles\%USERNAME%\data.json");
    }

    private Serilog.Core.Logger Logger(
        long maxBytes,
        int files,
        UserPathRedactor? redactor = null
    ) =>
        new LoggerConfiguration()
            .WriteTo.Sink(
                new FixedNameRollingFileSink(
                    LogFile,
                    maxBytes,
                    files,
                    new MessageTemplateTextFormatter(
                        "{Message:lj}{NewLine}{Exception}",
                        CultureInfo.InvariantCulture
                    ),
                    redactor ?? new UserPathRedactor(null, null)
                )
            )
            .CreateLogger();
}
