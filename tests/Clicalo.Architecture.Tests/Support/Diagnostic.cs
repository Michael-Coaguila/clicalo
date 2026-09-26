namespace Clicalo.Architecture.Tests.Support;

/// <summary>An MSBuild diagnostic.</summary>
internal sealed record Diagnostic(
    string File,
    int Line,
    string Severity,
    string Code,
    string Message,
    string Project
);
