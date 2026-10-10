using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace Clicalo.Build;

/// <summary>The steps of the M2 verbs: <c>cl run</c>, <c>cl note</c> and <c>cl perf</c> (blueprint §13).</summary>
internal sealed partial class BuildSteps
{
    /// <summary>The app project.</summary>
    public const string AppProject = "src/Clicalo.App/Clicalo.App.csproj";

    /// <summary>The guardian project, published next to Clicalo.exe.</summary>
    public const string SentinelProject = "src/Clicalo.Sentinel/Clicalo.Sentinel.csproj";

    /// <summary>The performance measurements.</summary>
    public const string PerformanceProject = "tests/Clicalo.Performance/Clicalo.Performance.csproj";

    /// <summary>How long <c>cl run</c> watches the app before saying it is running.</summary>
    public static readonly TimeSpan LaunchWatch = TimeSpan.FromSeconds(5);

    private const string NoInput = "--no-input";

    /// <summary>The runtime identifier of this machine (<c>win-x64</c> or <c>win-arm64</c>).</summary>
    public static string RuntimeIdentifier =>
        RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "win-arm64" : "win-x64";

    /// <summary>
    /// <c>cl run</c>: starts the Debug build of Clicalo.exe with its data in <paramref name="dataDirectory"/> and
    /// without key sending (<c>--no-input</c>): on a developer's machine nothing is ever injected. The app keeps
    /// running; it fails the step only if it ends within <see cref="LaunchWatch"/>.
    /// </summary>
    public Task LaunchAsync(string dataDirectory) =>
        context.Steps.RunAsync(
            "run",
            Messages.RunPurpose,
            async () =>
            {
                var executable = Path.Combine(
                    layout.Artifacts,
                    "bin",
                    "Clicalo.App",
                    "debug",
                    "Clicalo.exe"
                );
                if (!File.Exists(executable))
                {
                    throw new StepFailedException(
                        new FailureDetails
                        {
                            Summary = Messages.RunMissing(layout.RelativeForward(executable)),
                            Hint = Messages.RunHint,
                        }
                    );
                }

                Directory.CreateDirectory(dataDirectory);
                var start = new ProcessStartInfo(executable) { UseShellExecute = false };
                start.ArgumentList.Add(NoInput);
                start.ArgumentList.Add("--data");
                start.ArgumentList.Add(dataDirectory);
                using var process =
                    Process.Start(start)
                    ?? throw new StepFailedException(
                        new FailureDetails
                        {
                            Summary = Messages.RunNotStarted,
                            Hint = Messages.RunHint,
                        }
                    );
                using var watch = new CancellationTokenSource(LaunchWatch);
                try
                {
                    await process.WaitForExitAsync(watch.Token);
                }
                catch (OperationCanceledException)
                {
                    // Still running after the watch: started.
                    context.AddNote(Messages.RunStarted(dataDirectory));
                    return;
                }

                if (process.ExitCode == 0)
                {
                    // A second start shows the instance that was already running (SIS-003).
                    context.AddNote(Messages.RunAlreadyOpen);
                    return;
                }

                throw new StepFailedException(
                    new FailureDetails
                    {
                        Summary = Messages.RunEnded(process.ExitCode),
                        Command = CommandRunner.Display(executable, start.ArgumentList),
                        ExitCode = process.ExitCode,
                        Hint = Messages.RunHint,
                    }
                );
            }
        );

    /// <summary>
    /// <c>cl note</c>: creates the user-facing news fragment of the current branch in <c>changes/unreleased/</c>
    /// (blueprint §11, ACT-004), in Spanish and English, and opens it; an existing one is only opened.
    /// </summary>
    public Task NoteAsync() =>
        context.Steps.RunAsync(
            "note",
            Messages.NotePurpose,
            async () =>
            {
                var branch = await CommandRunner.ReadAsync(
                    "git",
                    ["rev-parse", "--abbrev-ref", "HEAD"]
                );
                var name = NoteName(branch.ExitCode == 0 ? branch.StandardOutput : string.Empty);
                var folder = Path.Combine(layout.Root, "changes", "unreleased");
                var file = Path.Combine(folder, name + ".yml");
                if (File.Exists(file))
                {
                    context.AddNote(Messages.NoteExists(layout.RelativeForward(file)));
                }
                else
                {
                    Directory.CreateDirectory(folder);
                    await File.WriteAllTextAsync(file, NoteTemplate, new UTF8Encoding(false));
                    context.AddNote(Messages.NoteCreated(layout.RelativeForward(file)));
                }

                await ErrorFileOpener.TryOpenAsync(file);
            }
        );

    /// <summary>
    /// <c>cl perf</c>, first step: publishes every <see cref="PublishVariant"/> of Clicalo.exe (and Sentinel next to it)
    /// in Release under <paramref name="output"/>, with the same restore as any build: the lock files hold the graphs of
    /// both shipped runtimes, and in CI the restore is locked (<see cref="PublishArguments"/>).
    /// </summary>
    public Task PublishAsync(IReadOnlyList<PublishVariant> variants, string output) =>
        context.Steps.RunAsync(
            "publish",
            Messages.PublishPurpose,
            async () =>
            {
                foreach (var variant in variants)
                {
                    var folder = Path.Combine(output, variant.Name);
                    if (Directory.Exists(folder))
                    {
                        Directory.Delete(folder, recursive: true);
                    }

                    await PublishProjectAsync(AppProject, folder, variant.Properties);
                    await PublishProjectAsync(SentinelProject, folder, []);
                }
            }
        );

    /// <summary>Adds to the final line where the numbers of <c>cl perf</c> are.</summary>
    public void NotePerfResults(string output)
    {
        var report = Path.Combine(output, "s5.md");
        if (File.Exists(report))
        {
            context.AddNote(Messages.PerfReport(layout.RelativeForward(report)));
        }
    }

    /// <summary>The <c>CLICALO_PERF_APPS</c> value for the published <paramref name="variants"/>.</summary>
    public static string PerfApps(IReadOnlyList<PublishVariant> variants, string output) =>
        string.Join(
            ';',
            variants.Select(variant =>
                variant.Name + "=" + Path.Combine(output, variant.Name, "Clicalo.exe")
            )
        );

    /// <summary>
    /// The file name of the note of <paramref name="branch"/>: lower case, letters, digits and hyphens
    /// (<c>feat/panel-tray</c> → <c>feat-panel-tray</c>); a date when there is no usable branch.
    /// </summary>
    internal static string NoteName(string branch)
    {
        var name = new StringBuilder();
        foreach (var character in branch.Trim().ToLowerInvariant())
        {
            name.Append(char.IsAsciiLetterOrDigit(character) ? character : '-');
        }

        var clean = string.Join(
            '-',
            name.ToString().Split('-', StringSplitOptions.RemoveEmptyEntries)
        );
        return clean.Length == 0 || clean is "head" or "main"
            ? "note-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)
            : clean;
    }

    /// <summary>The fragment <c>cl note</c> writes: one sentence per language, in the user's words.</summary>
    internal const string NoteTemplate =
        "# Novedad para usuarios (ACT-004, blueprint §11): la app la muestra y viaja en el manifiesto firmado.\n"
        + "# Una frase por idioma, en palabras de usuario, sin jerga ni nombres internos.\n"
        + "type: feat # feat, fix, a11y o perf\n"
        + "es: \"\"\n"
        + "en: \"\"\n";

    /// <summary>
    /// The <c>dotnet publish</c> arguments of <paramref name="project"/> for <paramref name="runtime"/> (this machine's
    /// when null). The runtime goes in
    /// <c>ClicaloRuntimeIdentifier</c>, which only the published executables turn into their <c>RuntimeIdentifier</c>
    /// (Directory.Build.props): <c>-r</c> is a global property that would reach the restore of every library, generator
    /// and analyzer they reference, whose lock files hold no runtime graph, and fail it with NU1004 in locked mode.
    /// </summary>
    internal static List<string> PublishArguments(
        string project,
        string folder,
        IReadOnlyList<string> properties,
        string? runtime = null
    ) =>
        [
            "publish",
            project,
            "-c",
            Release,
            "-p:ClicaloRuntimeIdentifier=" + (runtime ?? RuntimeIdentifier),
            "-o",
            folder,
            .. properties,
        ];

    private async Task PublishProjectAsync(
        string project,
        string folder,
        IReadOnlyList<string> properties,
        string? runtime = null
    )
    {
        var log = PrepareErrorLog("publish");
        var args = PublishArguments(project, layout.Relative(folder), properties, runtime);
        AddMsBuildSwitches(args, log);
        var exitCode = await RunAsync(args);
        if (exitCode != 0)
        {
            throw MsBuildFailure(
                Messages.PublishFailed,
                Messages.PublishSection,
                Messages.PublishHint,
                args,
                exitCode,
                log
            );
        }
    }
}
