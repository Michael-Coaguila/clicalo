using System.Collections.Immutable;

namespace Clicalo.Architecture.Tests.Support;

/// <summary>Exit code, full output and parsed diagnostics of a CLI run.</summary>
internal sealed record CliResult(
    int ExitCode,
    string Output,
    ImmutableArray<Diagnostic> Diagnostics
);
