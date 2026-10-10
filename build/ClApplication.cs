using System.Text;
using Bullseye;

namespace Clicalo.Build;

/// <summary>
/// Parses the verbs, runs them with Bullseye and always ends with exactly one line that Narrator can read:
/// what ran, whether it worked, how long it took, and where the details are when it did not.
/// </summary>
internal sealed class ClApplication(
    RepoLayout layout,
    TextWriter output,
    TimeProvider time,
    IReadOnlyCollection<string> keepOnClean
)
{
    /// <summary>The time <c>cl fast</c> should stay under (blueprint §13).</summary>
    public static readonly TimeSpan FastBudget = TimeSpan.FromSeconds(45);

    /// <summary>Exit code for a verb that failed or is not available yet.</summary>
    public const int FailureExitCode = 1;

    /// <summary>Exit code for an unknown verb or invalid options.</summary>
    public const int UsageExitCode = 2;

    // Reports and summaries use LF regardless of the platform (.editorconfig).
    private const char NewLine = '\n';

    /// <summary>Runs <c>cl</c> with <paramref name="args"/> and returns the process exit code.</summary>
    public async Task<int> RunAsync(IReadOnlyList<string> args)
    {
        var (clArgs, devCliArgs) = VerbCatalog.SplitArguments(args);
        var (verbs, options, unknownOptions, showHelp) = CommandLine.Parse(clArgs);
        var informational =
            showHelp
            || options.ListTargets
            || options.ListTree
            || options.ListDependencies
            || options.ListInputs;
        var command = string.Join(' ', verbs.Concat(devCliArgs));

        if (verbs.Count == 0 && !informational && unknownOptions.Count == 0)
        {
            await WriteVerbListAsync(listFirst: true);
            return 0;
        }

        foreach (var verb in verbs)
        {
            if (VerbCatalog.FindFuture(verb) is { } future)
            {
                await output.WriteLineAsync(Messages.NotYetAvailable(verb, future.Milestone));
                return FailureExitCode;
            }

            if (!VerbCatalog.IsAvailable(verb))
            {
                await output.WriteLineAsync(
                    Messages.UnknownVerb(verb, Messages.JoinList(VerbCatalog.Available))
                );
                return UsageExitCode;
            }
        }

        // Screen readers spell out box-drawing characters; Bullseye can do without them.
        options.NoExtendedChars = true;

        var mode = OutputMode.Detect(options.Verbose);
        var context = new RunContext(output, mode);
        var steps = new BuildSteps(layout, context);
        var targets = DefineTargets(new ClVerbs(steps, layout, keepOnClean, devCliArgs));

        // Bullseye's own start, finish and summary-table lines repeat what the step lines and the final line
        // say; they are kept only where they are the point (listings, help, dry runs, --verbose).
        var bullseyeOutput =
            informational || options.DryRun || options.Verbose ? output : TextWriter.Null;

        if (!informational && !options.DryRun)
        {
            DeleteStaleReport();
        }

        var started = time.GetTimestamp();
        try
        {
            await targets.RunWithoutExitingAsync(
                verbs,
                options,
                unknownOptions,
                showHelp,
                messageOnly: exception => exception is StepFailedException,
                getMessagePrefix: () => "cl",
                outputWriter: bullseyeOutput,
                diagnosticsWriter: bullseyeOutput
            );
        }
        catch (InvalidUsageException exception)
        {
            await output.WriteLineAsync(
                Messages.InvalidUsage(
                    command.Length == 0 ? string.Join(' ', args) : command,
                    exception.Message
                )
            );
            return UsageExitCode;
        }
        catch (TargetFailedException)
        {
            var elapsed = time.GetElapsedTime(started);
            var failure =
                context.Steps.Failure
                ?? new StepFailure(
                    command,
                    new FailureDetails { Summary = Messages.UnexpectedFailed }
                );
            var report = await WriteReportAsync(command, failure, elapsed);
            var line = Messages.Failure(
                command,
                failure.Step,
                layout.Relative(layout.LastErrorFile)
            );
            if (mode.GitHubActions)
            {
                // An annotation puts the same sentence on the pull request's checks page.
                await output.WriteLineAsync("::error title=cl " + command + "::" + line);
            }

            await AppendJobSummaryAsync(report);
            await output.WriteLineAsync(line);
            await ErrorFileOpener.TryOpenAsync(layout.LastErrorFile);
            return FailureExitCode;
        }

        if (informational || verbs.Count == 0)
        {
            await WriteVerbListAsync(listFirst: false);
            return 0;
        }

        if (options.DryRun)
        {
            await output.WriteLineAsync(Messages.DryRun(command));
            return 0;
        }

        var duration = time.GetElapsedTime(started);
        var notes = context.Notes.ToList();
        if (verbs is [VerbCatalog.Fast] && duration > FastBudget)
        {
            notes.Add(Messages.OverBudget(SpokenDuration.Format(FastBudget)));
        }

        var success = Messages.WithNotes(
            Messages.Success(command, SpokenDuration.Format(duration)),
            notes
        );
        await AppendJobSummaryAsync(Markdown.Text(success) + NewLine);
        await output.WriteLineAsync(success);
        return 0;
    }

    /// <summary>
    /// In GitHub Actions, adds the outcome to the job summary: the final line on success, the whole report
    /// on failure, so the errors are readable on the run page without downloading anything.
    /// </summary>
    private static async Task AppendJobSummaryAsync(string markdown)
    {
        var summary = Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY");
        if (string.IsNullOrEmpty(summary))
        {
            return;
        }

        await File.AppendAllTextAsync(summary, markdown + NewLine, new UTF8Encoding(false));
    }

    private static Targets DefineTargets(ClVerbs verbs)
    {
        var targets = new Targets();
        targets.Add(VerbCatalog.Setup, Messages.SetupDescription, verbs.SetupAsync);
        targets.Add(VerbCatalog.Build, Messages.BuildDescription, verbs.BuildAsync);
        targets.Add(VerbCatalog.Fast, Messages.FastDescription, verbs.FastAsync);
        targets.Add(VerbCatalog.Test, Messages.TestDescription, verbs.TestAsync);
        targets.Add(VerbCatalog.Desk, Messages.DeskDescription, verbs.DeskAsync);
        targets.Add(VerbCatalog.Fix, Messages.FixDescription, verbs.FixAsync);
        targets.Add(VerbCatalog.Check, Messages.CheckDescription, verbs.CheckAsync);
        targets.Add(VerbCatalog.Clean, Messages.CleanDescription, verbs.CleanAsync);
        targets.Add(VerbCatalog.I18nCheck, Messages.I18nCheckDescription, verbs.I18nCheckAsync);
        targets.Add(VerbCatalog.I18nImport, Messages.I18nImportDescription, verbs.I18nImportAsync);
        targets.Add(VerbCatalog.AdrCheck, Messages.AdrCheckDescription, verbs.AdrCheckAsync);
        targets.Add(VerbCatalog.Trace, Messages.TraceDescription, verbs.TraceAsync);
        targets.Add(VerbCatalog.Run, Messages.RunDescription, verbs.RunAppAsync);
        targets.Add(VerbCatalog.Note, Messages.NoteDescription, verbs.NoteAsync);
        targets.Add(VerbCatalog.Perf, Messages.PerfDescription, verbs.PerfAsync);
        targets.Add(VerbCatalog.Quarantine, Messages.QuarantineDescription, verbs.QuarantineAsync);
        targets.Add(VerbCatalog.Package, Messages.PackageDescription, verbs.PackageAsync);
        foreach (var future in VerbCatalog.Future)
        {
            // Listed for discoverability; RunAsync answers "available in Mx" before Bullseye runs anything.
            targets.Add(
                future.Name,
                Messages.FutureDescription(future.Milestone),
                () => throw new InvalidOperationException(future.Name + " is not available yet.")
            );
        }

        return targets;
    }

    private async Task WriteVerbListAsync(bool listFirst)
    {
        if (listFirst)
        {
            var width = VerbCatalog.Available.Max(verb => verb.Length) + 2;
            foreach (var verb in VerbCatalog.Available)
            {
                await output.WriteLineAsync("  " + verb.PadRight(width) + DescriptionOf(verb));
            }
        }

        await output.WriteLineAsync(Messages.VerbList(Messages.JoinList(VerbCatalog.Available)));
    }

    private static string DescriptionOf(string verb) =>
        verb switch
        {
            VerbCatalog.Setup => Messages.SetupDescription,
            VerbCatalog.Build => Messages.BuildDescription,
            VerbCatalog.Fast => Messages.FastDescription,
            VerbCatalog.Test => Messages.TestDescription,
            VerbCatalog.Desk => Messages.DeskDescription,
            VerbCatalog.Fix => Messages.FixDescription,
            VerbCatalog.Check => Messages.CheckDescription,
            VerbCatalog.Clean => Messages.CleanDescription,
            VerbCatalog.I18nCheck => Messages.I18nCheckDescription,
            VerbCatalog.I18nImport => Messages.I18nImportDescription,
            VerbCatalog.AdrCheck => Messages.AdrCheckDescription,
            VerbCatalog.Trace => Messages.TraceDescription,
            VerbCatalog.Run => Messages.RunDescription,
            VerbCatalog.Note => Messages.NoteDescription,
            VerbCatalog.Perf => Messages.PerfDescription,
            VerbCatalog.Quarantine => Messages.QuarantineDescription,
            VerbCatalog.Package => Messages.PackageDescription,
            _ => string.Empty,
        };

    private void DeleteStaleReport()
    {
        if (File.Exists(layout.LastErrorFile))
        {
            File.Delete(layout.LastErrorFile);
        }
    }

    private async Task<string> WriteReportAsync(
        string command,
        StepFailure failure,
        TimeSpan elapsed
    )
    {
        Directory.CreateDirectory(layout.ClDirectory);
        var report = FailureReport.Render(command, failure, elapsed, time.GetLocalNow());
        await File.WriteAllTextAsync(layout.LastErrorFile, report, new UTF8Encoding(false));
        return report;
    }
}
