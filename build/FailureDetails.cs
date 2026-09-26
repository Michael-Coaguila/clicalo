namespace Clicalo.Build;

/// <summary>What went wrong in a step, with enough detail to fix it without reading the console.</summary>
internal sealed record FailureDetails
{
    /// <summary>One sentence, shown first in the report and by Bullseye in the console.</summary>
    public required string Summary { get; init; }

    /// <summary>The process that failed, as it could be typed again; <see langword="null"/> if none.</summary>
    public string? Command { get; init; }

    /// <summary>Exit code of <see cref="Command"/>, when there was a process.</summary>
    public int? ExitCode { get; init; }

    /// <summary>Human meaning of <see cref="ExitCode"/> when the tool documents it.</summary>
    public string? ExitCodeMeaning { get; init; }

    /// <summary>Detailed sections (errors, failed tests, files).</summary>
    public IReadOnlyList<ReportSection> Sections { get; init; } = [];

    /// <summary>What to do next, as plain text.</summary>
    public string? Hint { get; init; }

    /// <summary>Describes a defect of <c>cl</c> itself.</summary>
    public static FailureDetails FromException(Exception exception) =>
        new()
        {
            Summary = Messages.UnexpectedFailed,
            Sections =
            [
                new ReportSection(
                    Messages.UnexpectedSection,
                    Markdown.CodeBlock(exception.ToString())
                ),
            ],
            Hint = Messages.UnexpectedHint,
        };
}
