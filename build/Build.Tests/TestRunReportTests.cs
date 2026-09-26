using System.Globalization;
using System.Xml.Linq;

namespace Clicalo.Build.Tests;

public sealed class TestRunReportTests
{
    private const string Trx = """
        <?xml version="1.0" encoding="utf-8"?>
        <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
          <Results>
            <UnitTestResult testName="Clicalo.Domain.Tests.Library.Adds_a_shortcut" outcome="Passed" />
            <UnitTestResult testName="Clicalo.Domain.Tests.Library.Rejects_duplicates" outcome="Failed">
              <Output>
                <ErrorInfo>
                  <Message>Shouldly.ShouldAssertException : count should be 1 but was 2</Message>
                  <StackTrace>   at Clicalo.Domain.Tests.Library.Rejects_duplicates() in Library.cs:line 42</StackTrace>
                </ErrorInfo>
              </Output>
            </UnitTestResult>
            <UnitTestResult testName="Clicalo.Domain.Tests.Library.Needs_a_desktop" outcome="NotExecuted" />
            <UnitTestResult testName="Clicalo.Domain.Tests.Library.Hangs" outcome="Timeout" />
          </Results>
        </TestRun>
        """;

    private static TestRunReport Parse(params string[] documents) =>
        TestRunReport.Parse(
            documents.Select((text, index) => (FileName(index), XDocument.Parse(text))),
            documents.Select((_, index) => FileName(index)).ToList()
        );

    private static string FileName(int index) =>
        index.ToString(CultureInfo.InvariantCulture) + ".trx";

    [Fact]
    public void Counts_tests_that_ran_and_ignores_skipped_ones() => Parse(Trx).Executed.ShouldBe(3);

    [Fact]
    public void Lists_failed_and_timed_out_tests_with_message_and_stack()
    {
        var report = Parse(Trx);

        report.Failures.Count.ShouldBe(2);
        var failure = report.Failures[0];
        failure.Name.ShouldBe("Clicalo.Domain.Tests.Library.Rejects_duplicates");
        failure.Outcome.ShouldBe("Failed");
        failure.Message.ShouldBe("Shouldly.ShouldAssertException : count should be 1 but was 2");
        failure.StackTrace.ShouldNotBeNull().ShouldContain("Library.cs:line 42");
        report.Failures[1].Outcome.ShouldBe("Timeout");
        report.Failures[1].Message.ShouldBeNull();
    }

    [Fact]
    public void Adds_up_several_test_modules() => Parse(Trx, Trx).Executed.ShouldBe(6);

    [Fact]
    public void Renders_one_heading_per_failed_test_with_code_blocks()
    {
        var markdown = Parse(Trx).RenderFailures();

        markdown.ShouldContain("### Clicalo.Domain.Tests.Library.Rejects_duplicates\n");
        markdown.ShouldContain(
            "```text\nShouldly.ShouldAssertException : count should be 1 but was 2\n```"
        );
        markdown.ShouldContain("### Clicalo.Domain.Tests.Library.Hangs\n");
    }

    [Fact]
    public void A_missing_results_folder_means_no_tests() =>
        TestRunReport
            .Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")))
            .Executed.ShouldBe(0);

    [Fact]
    public void Describes_the_count_for_the_final_line() =>
        Parse(Trx).DescribeCount().ShouldBe("3 pruebas");
}
