namespace Clicalo.Build.Tests;

/// <summary>
/// The M2 exit criterion «tocar → SendInput en InputProbe con p95 ≤ 50 ms» (blueprint §10.3, §14; NFR-001) is enforced by
/// a run, not only measured: the <c>perf</c> job of nightly.yml runs <c>cl perf</c> every night, fails when a budget with
/// the gate <c>everyRun</c> is broken and publishes the numbers, and lab.yml repeats it on the touch lab with every budget
/// gating (<c>CLICALO_PERF_GATE=1</c>). Every nightly job must be green before a release. A <c>continue-on-error</c>
/// would turn the gate back into a trend, so no job has one.
/// </summary>
/// <remarks>
/// The tiers (testing-strategy.md): a pull request is gated only by deterministic tests (<c>verify</c> = <c>cl check</c>);
/// the desktop, the measurements, chaos and the quarantine run in nightly.yml, which reports a scheduled failure as an
/// issue from the only job allowed to write issues.
/// </remarks>
[Trait("Req", "NFR-001")]
public sealed class WorkflowGateTests
{
    private static readonly string Workflows = Path.Combine(
        RepoLayout.Locate(AppContext.BaseDirectory).Root,
        ".github",
        "workflows"
    );

    [Fact]
    public void A_pull_request_is_gated_only_by_the_deterministic_tier()
    {
        var jobs = Section(File.ReadAllText(Path.Combine(Workflows, "pr.yml")), "jobs");

        jobs.ShouldNotContain("  desk:", StringComparer.Ordinal);
        jobs.ShouldNotContain("  perf:", StringComparer.Ordinal);
        Job("pr.yml", "verify").ShouldContain(@"run: .\cl.cmd check");
        Job("pr.yml", "verify-arm64").ShouldContain(@"run: .\cl.cmd check");
    }

    [Fact]
    public void The_nightly_tier_runs_every_day_and_by_hand_on_any_ref()
    {
        var text = File.ReadAllText(Path.Combine(Workflows, "nightly.yml"));
        var on = string.Join('\n', Section(text, "on"));

        on.ShouldContain("  schedule:");
        on.ShouldContain("  workflow_dispatch:");
        on.ShouldContain("      ref:");
        Job("nightly.yml", "desk").ShouldContain(@"run: .\cl.cmd desk");
        Job("nightly.yml", "quarantine").ShouldContain(@"run: .\cl.cmd quarantine");
        foreach (var job in new[] { "desk", "perf", "quarantine" })
        {
            Job("nightly.yml", job).ShouldContain("ref: ${{ inputs.ref }}");
            Job("nightly.yml", job).ShouldContain("persist-credentials: false");
        }
    }

    [Fact]
    public void Only_the_report_job_of_the_nightly_tier_writes_issues_and_only_for_a_scheduled_failure()
    {
        var text = File.ReadAllText(Path.Combine(Workflows, "nightly.yml"));
        var report = Job("nightly.yml", "report");

        Section(text, "permissions").ShouldBe(["permissions:", "  contents: read"]);
        text.Split("issues: write").Length.ShouldBe(2, "issues: write appears only once");
        report.ShouldContain("issues: write");
        report.ShouldContain("needs: [desk, perf, quarantine]");
        report.ShouldContain("if: ${{ failure() && github.event_name == 'schedule' }}");
        report.ShouldContain("--label nightly");
        report.ShouldNotContain("actions/checkout");
    }

    [Fact]
    public void The_perf_job_of_the_nightly_tier_gates_and_publishes_the_numbers()
    {
        var job = Job("nightly.yml", "perf");

        job.ShouldContain(@"run: .\cl.cmd perf");
        job.ShouldContain("CI: true");
        job.ShouldNotContain("continue-on-error");
        job.ShouldContain("GITHUB_STEP_SUMMARY");
        job.ShouldContain("artifacts/perf/*.json");
        job.ShouldContain("artifacts/perf/*.md");
        Steps(job)
            .Where(static step =>
                step.Contains("GITHUB_STEP_SUMMARY", StringComparison.Ordinal)
                || step.Contains("upload-artifact", StringComparison.Ordinal)
            )
            .ShouldAllBe(static step => step.Contains("if: always()", StringComparison.Ordinal));
    }

    [Fact]
    public void The_lab_runs_every_budget_as_a_gate_on_the_touch_machine_only()
    {
        var text = File.ReadAllText(Path.Combine(Workflows, "lab.yml"));
        var perf = Job("lab.yml", "perf");

        Section(text, "on").ShouldBe(["on:", "  workflow_dispatch:"]);
        perf.ShouldContain(@"run: .\cl.cmd perf");
        perf.ShouldContain("CLICALO_PERF_GATE: 1");
        perf.ShouldContain("runs-on: [self-hosted, Windows, lab]");
        Job("lab.yml", "desk").ShouldContain("runs-on: [self-hosted, Windows, lab]");
        perf.ShouldContain("GITHUB_STEP_SUMMARY");
    }

    [Theory]
    [InlineData("pr.yml")]
    [InlineData("nightly.yml")]
    [InlineData("lab.yml")]
    [InlineData("s0.yml")]
    public void No_job_turns_its_failure_into_a_success(string workflow) =>
        File.ReadAllLines(Path.Combine(Workflows, workflow))
            .Where(static line => !line.TrimStart().StartsWith('#'))
            .ShouldNotContain(static line =>
                line.Contains("continue-on-error", StringComparison.Ordinal)
            );

    [Fact]
    public void Only_the_lab_uses_the_self_hosted_touch_machine()
    {
        foreach (var workflow in Directory.GetFiles(Workflows, "*.yml"))
        {
            if (!string.Equals(Path.GetFileName(workflow), "lab.yml", StringComparison.Ordinal))
            {
                File.ReadAllText(workflow)
                    .ShouldNotContain("self-hosted", customMessage: Path.GetFileName(workflow));
            }
        }
    }

    /// <summary>The lines of <paramref name="job"/> under <c>jobs:</c>, from its key to the next job.</summary>
    private static string Job(string workflow, string job)
    {
        var lines = Section(File.ReadAllText(Path.Combine(Workflows, workflow)), "jobs");
        var start = lines.IndexOf("  " + job + ":");
        start.ShouldBeGreaterThanOrEqualTo(0, workflow + " has the job " + job);
        var end = start + 1;
        while (end < lines.Count && !IsKey(lines[end], indent: 2))
        {
            end++;
        }

        return string.Join('\n', lines.GetRange(start, end - start));
    }

    /// <summary>A top-level section: its key line and every indented, blank or comment line after it.</summary>
    private static List<string> Section(string text, string key)
    {
        var lines = text.ReplaceLineEndings("\n").Split('\n');
        var start = Array.IndexOf(lines, key + ":");
        start.ShouldBeGreaterThanOrEqualTo(0, "the workflow has «" + key + ":»");
        var section = new List<string> { lines[start] };
        for (var i = start + 1; i < lines.Length && !IsKey(lines[i], indent: 0); i++)
        {
            if (lines[i].Trim().Length > 0 && !lines[i].TrimStart().StartsWith('#'))
            {
                section.Add(lines[i]);
            }
        }

        return section;
    }

    /// <summary>The steps of a job, one string each.</summary>
    private static IEnumerable<string> Steps(string job) =>
        job.Split("\n      - ", StringSplitOptions.None).Skip(1);

    private static bool IsKey(string line, int indent) =>
        line.Length > indent
        && line[..indent].All(static c => c == ' ')
        && char.IsAsciiLetterOrDigit(line[indent])
        && line.TrimEnd().EndsWith(':');
}
