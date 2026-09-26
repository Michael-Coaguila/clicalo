using System.Text;
using System.Xml.Linq;

namespace Clicalo.Build;

/// <summary>
/// The outcome of a test run, read from the TRX files that xUnit writes (one per test module,
/// <see cref="BuildSteps.TrxReportProperty"/>). It counts what <c>dotnet test</c> counts in its summary: every test
/// result is in the total, and the skipped ones are also counted apart.
/// </summary>
internal sealed class TestRunReport
{
    /// <summary>How many failed tests the report details before pointing to the TRX files.</summary>
    public const int MaxListed = 30;

    private static readonly XNamespace Trx =
        "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

    private static readonly HashSet<string> FailedOutcomes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Failed",
        "Error",
        "Timeout",
        "Aborted",
    };

    private TestRunReport(
        int total,
        int skipped,
        IReadOnlyList<FailedTest> failures,
        IReadOnlyList<string> files
    )
    {
        Total = total;
        Skipped = skipped;
        Failures = failures;
        Files = files;
    }

    /// <summary>Every test result, as the <c>total</c> of <c>dotnet test</c>: passed, failed and skipped.</summary>
    public int Total { get; }

    /// <summary>Tests that did not run (<c>NotExecuted</c>), the <c>skipped</c> of <c>dotnet test</c>.</summary>
    public int Skipped { get; }

    /// <summary>Tests that ran (passed or failed).</summary>
    public int Executed => Total - Skipped;

    /// <summary>Tests that did not pass.</summary>
    public IReadOnlyList<FailedTest> Failures { get; }

    /// <summary>The TRX files that were read.</summary>
    public IReadOnlyList<string> Files { get; }

    /// <summary>Reads every <c>*.trx</c> below <paramref name="directory"/>.</summary>
    public static TestRunReport Load(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return new TestRunReport(0, 0, [], []);
        }

        var files = Directory
            .EnumerateFiles(directory, "*.trx", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .ToList();
        return Parse(files.Select(file => (file, XDocument.Load(file))), files);
    }

    /// <summary>Builds a report from already loaded TRX documents.</summary>
    public static TestRunReport Parse(
        IEnumerable<(string File, XDocument Document)> documents,
        IReadOnlyList<string> files
    )
    {
        var total = 0;
        var skipped = 0;
        var failures = new List<FailedTest>();
        foreach (var (_, document) in documents)
        {
            foreach (var result in document.Descendants(Trx + "UnitTestResult"))
            {
                total++;
                var outcome = (string?)result.Attribute("outcome") ?? string.Empty;
                if (string.Equals(outcome, "NotExecuted", StringComparison.OrdinalIgnoreCase))
                {
                    skipped++;
                    continue;
                }

                if (!FailedOutcomes.Contains(outcome))
                {
                    continue;
                }

                var errorInfo = result.Element(Trx + "Output")?.Element(Trx + "ErrorInfo");
                failures.Add(
                    new FailedTest(
                        (string?)result.Attribute("testName") ?? string.Empty,
                        outcome,
                        NullIfBlank(errorInfo?.Element(Trx + "Message")?.Value),
                        NullIfBlank(errorInfo?.Element(Trx + "StackTrace")?.Value)
                    )
                );
            }
        }

        return new TestRunReport(total, skipped, failures, files);
    }

    /// <summary>Renders the failed tests: one third-level heading per test, message and stack.</summary>
    public string RenderFailures()
    {
        var text = new StringBuilder();
        foreach (var failure in Failures.Take(MaxListed))
        {
            text.Append("### ").Append(Markdown.Text(failure.Name)).Append("\n\n");
            if (failure.Message is not null)
            {
                text.Append(Messages.TestMessageLabel).Append(":\n\n");
                text.Append(Markdown.CodeBlock(failure.Message)).Append("\n\n");
            }

            if (failure.StackTrace is not null)
            {
                text.Append(Messages.TestStackLabel).Append(":\n\n");
                text.Append(Markdown.CodeBlock(failure.StackTrace)).Append("\n\n");
            }
        }

        if (Failures.Count > MaxListed)
        {
            text.Append(Messages.TruncatedItems(MaxListed, Failures.Count)).Append('\n');
        }

        return text.ToString();
    }

    /// <summary>Lists the TRX files relative to the repository root.</summary>
    public string RenderFiles(RepoLayout layout)
    {
        var text = new StringBuilder();
        foreach (var file in Files)
        {
            text.Append("- ").Append(Markdown.Text(layout.RelativeForward(file))).Append('\n');
        }

        return text.ToString();
    }

    /// <summary>The total as a sentence fragment, for the final line, with the skipped tests when there are any.</summary>
    public string DescribeCount() => Messages.TestCount(Total, Skipped);

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
