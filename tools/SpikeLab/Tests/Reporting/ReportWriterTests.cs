using Clicalo.Tools.SpikeLab.Reporting;
using Clicalo.Tools.SpikeLab.Scripting;

namespace Clicalo.Tools.SpikeLab.Tests.Reporting;

public sealed class ReportWriterTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "clicalo-spikelab-tests-" + Guid.NewGuid().ToString("N")
    );

    [Fact]
    public void Files_are_named_after_the_spike_and_the_local_start_time()
    {
        var start = new DateTimeOffset(
            2026,
            9,
            26,
            10,
            15,
            30,
            TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 9, 26))
        );

        ReportWriter.BaseName(SpikeId.S4, start).ShouldBe("S4-2026-09-26-101530");
    }

    [Fact]
    public void The_default_folder_is_under_local_application_data() =>
        ReportWriter.DefaultDirectory.ShouldBe(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Clicalo.SpikeLab",
                "reports"
            )
        );

    [Fact]
    public async Task Both_files_are_written_and_replaced_without_leftovers()
    {
        using var writer = new ReportWriter(_directory, SpikeId.S1, ReportFixture.Start);

        await writer.WriteAsync(
            "{\"a\":1}\n",
            "# Primero\n",
            TestContext.Current.CancellationToken
        );
        await writer.WriteAsync(
            "{\"a\":2}\n",
            "# Segundo\n",
            TestContext.Current.CancellationToken
        );

        (
            await File.ReadAllTextAsync(writer.JsonPath, TestContext.Current.CancellationToken)
        ).ShouldBe("{\"a\":2}\n");
        (
            await File.ReadAllTextAsync(writer.MarkdownPath, TestContext.Current.CancellationToken)
        ).ShouldBe("# Segundo\n");
        Path.GetFileName(writer.JsonPath).ShouldStartWith("S1-");
        Directory.GetFiles(_directory).Length.ShouldBe(2);
    }

    [Fact]
    public async Task A_second_run_started_in_the_same_second_gets_its_own_files()
    {
        using var first = new ReportWriter(_directory, SpikeId.S3, ReportFixture.Start);
        await first.WriteAsync("{}\n", "#\n", TestContext.Current.CancellationToken);

        using var second = new ReportWriter(_directory, SpikeId.S3, ReportFixture.Start);

        string.Equals(second.JsonPath, first.JsonPath, StringComparison.OrdinalIgnoreCase)
            .ShouldBeFalse();
        Path.GetFileNameWithoutExtension(second.JsonPath).ShouldEndWith("-2");
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
