using System.Globalization;
using System.Text;

namespace Clicalo.Build;

/// <summary>
/// Renders <c>artifacts/cl/last-error.md</c>: a title, one summary sentence, a short list of facts and one
/// section per kind of problem. Headings make it navigable with Narrator; nothing depends on colour.
/// </summary>
internal static class FailureReport
{
    /// <summary>Renders the report of <paramref name="failure"/> for <c>cl <paramref name="command"/></c>.</summary>
    public static string Render(
        string command,
        StepFailure failure,
        TimeSpan elapsed,
        DateTimeOffset now
    )
    {
        var details = failure.Details;
        var report = new StringBuilder();

        report
            .Append("# ")
            .Append(Markdown.Text(Messages.ReportTitle(command, failure.Step)))
            .Append("\n\n");
        report.Append(Markdown.Text(details.Summary)).Append("\n\n");

        AppendFact(report, Messages.ReportCommandLabel, Markdown.InlineCode("cl " + command));
        AppendFact(report, Messages.ReportStepLabel, Markdown.Text(failure.Step));
        if (details.Command is not null)
        {
            AppendFact(report, Messages.ReportProcessLabel, Markdown.InlineCode(details.Command));
        }

        if (details.ExitCode is { } exitCode)
        {
            var value = details.ExitCodeMeaning is null
                ? exitCode.ToString(CultureInfo.InvariantCulture)
                : string.Create(
                    CultureInfo.InvariantCulture,
                    $"{exitCode} ({details.ExitCodeMeaning})"
                );
            AppendFact(report, Messages.ReportExitCodeLabel, Markdown.Text(value));
        }

        AppendFact(report, Messages.ReportElapsedLabel, SpokenDuration.Format(elapsed));
        AppendFact(
            report,
            Messages.ReportDateLabel,
            now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                + " ("
                + Messages.ReportLocalTime
                + ")"
        );

        foreach (var section in details.Sections)
        {
            report.Append("\n## ").Append(Markdown.Text(section.Heading)).Append("\n\n");
            report.Append(Markdown.Lines(section.Body).TrimEnd('\n')).Append('\n');
        }

        if (details.Hint is not null)
        {
            report.Append("\n## ").Append(Messages.ReportWhatToDo).Append("\n\n");
            report.Append(Markdown.Text(details.Hint)).Append('\n');
        }

        return report.ToString();
    }

    private static void AppendFact(StringBuilder report, string label, string value) =>
        report.Append("- ").Append(label).Append(": ").Append(value).Append('\n');
}
