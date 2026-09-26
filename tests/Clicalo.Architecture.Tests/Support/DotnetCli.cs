using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Clicalo.Architecture.Tests.Support;

/// <summary>Runs the .NET CLI of the current SDK and parses the MSBuild diagnostics it prints.</summary>
internal static partial class DotnetCli
{
    /// <summary>Runs <c>dotnet</c> with English messages and without node reuse, and waits for it to exit.</summary>
    public static async Task<CliResult> RunAsync(
        string workingDirectory,
        IEnumerable<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken
    )
    {
        var start = new ProcessStartInfo(Host)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment["DOTNET_CLI_UI_LANGUAGE"] = "en";
        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        start.Environment["DOTNET_NOLOGO"] = "1";
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken
        );
        timeoutSource.CancelAfter(timeout);
        using var process =
            Process.Start(start) ?? throw new InvalidOperationException("Could not start " + Host);
        var output = process.StandardOutput.ReadToEndAsync(timeoutSource.Token);
        var error = process.StandardError.ReadToEndAsync(timeoutSource.Token);
        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        var text = await output + await error;
        return new CliResult(process.ExitCode, text, ParseDiagnostics(text));
    }

    /// <summary>The dotnet host that runs the tests (set by the CLI), or the one on the PATH.</summary>
    private static string Host =>
        Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } host
        && File.Exists(host)
            ? host
            : "dotnet";

    private static ImmutableArray<Diagnostic> ParseDiagnostics(string output) =>
        [
            .. output
                .Split('\n')
                .Select(line => DiagnosticLine().Match(line.TrimEnd('\r')))
                .Where(match => match.Success)
                .Select(match => new Diagnostic(
                    match.Groups["file"].Value.Trim(),
                    match.Groups["line"].Success
                        ? int.Parse(
                            match.Groups["line"].Value,
                            System.Globalization.CultureInfo.InvariantCulture
                        )
                        : 0,
                    match.Groups["severity"].Value,
                    match.Groups["code"].Value,
                    match.Groups["message"].Value,
                    match.Groups["project"].Value
                ))
                .Distinct(),
        ];

    // file(line,column): error CODE: message [project]  or  file : error CODE: message [project]
    [GeneratedRegex(
        @"^\s*(?<file>[^\r\n]+?)(?:\((?<line>\d+)(?:,\d+)*\))?\s*:\s*(?<severity>error|warning)\s+(?<code>[A-Za-z]+\d+)\s*:\s*(?<message>.*?)(?:\s+\[(?<project>[^\]]+)\])?$",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex DiagnosticLine();
}
