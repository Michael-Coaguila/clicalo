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

    private const string PassedOnly = """
        <?xml version="1.0" encoding="utf-8"?>
        <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
          <Results>
            <UnitTestResult testName="Clicalo.Domain.Tests.Library.Adds_a_shortcut" outcome="Passed" />
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
    public void Counts_what_dotnet_test_counts()
    {
        var report = Parse(Trx);

        report.Total.ShouldBe(
            4,
            "the total of dotnet test has every result, skipped ones included"
        );
        report.Skipped.ShouldBe(1);
        report.Executed.ShouldBe(3);
    }

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
    public void Adds_up_several_test_modules() => Parse(Trx, Trx).Total.ShouldBe(8);

    [Fact]
    public void Reads_the_results_file_of_every_module()
    {
        var folder = Directory.CreateTempSubdirectory("clicalo-trx-");
        try
        {
            File.WriteAllText(Path.Combine(folder.FullName, "Clicalo.Domain.Tests.trx"), Trx);
            File.WriteAllText(Path.Combine(folder.FullName, "Clicalo.Build.Tests.trx"), Trx);

            var report = TestRunReport.Load(folder.FullName);

            report.Files.Count.ShouldBe(2);
            report.Total.ShouldBe(8);
            report.Failures.Count.ShouldBe(4);
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public void Every_test_module_names_its_results_file_after_itself()
    {
        // xUnit's default name (user_machine_timestamp.trx) is taken when each module starts, and dotnet test starts
        // them within milliseconds of each other: two modules with one name keep only one module's results, which is
        // how cl can count fewer tests than dotnet test (1658 against 1772 in the M1 integration).
        var root = RepoLayout.Locate(AppContext.BaseDirectory).Root;
        var targets = File.ReadAllText(Path.Combine(root, "Directory.Build.targets"));

        BuildSteps.TrxReportProperty.ShouldBe("-p:ClicaloTrxReport=true");
        targets.ShouldContain(
            "<RunArguments Condition=\"'$(ClicaloTrxReport)' == 'true'\">--report-xunit-trx --report-xunit-trx-filename $(AssemblyName).trx</RunArguments>"
        );
    }

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
    public void Describes_the_count_for_the_final_line()
    {
        Parse(Trx).DescribeCount().ShouldBe("4 pruebas, 1 omitida");
        Parse(Trx, Trx).DescribeCount().ShouldBe("8 pruebas, 2 omitidas");
        Parse(PassedOnly).DescribeCount().ShouldBe("1 prueba");
    }
}
