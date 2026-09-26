namespace Clicalo.Build;

/// <summary>One error reported by MSBuild (compiler, analyzer, NuGet or MSBuild itself).</summary>
/// <param name="Origin">File path, or a tool name such as <c>CSC</c> when there is no file.</param>
/// <param name="Line">1-based line, when known.</param>
/// <param name="Column">1-based column, when known.</param>
/// <param name="Code">Diagnostic id such as <c>CS0103</c> or <c>NU3034</c>, when present.</param>
/// <param name="Message">The message, without the project suffix.</param>
/// <param name="Project">The project that reported it, when present.</param>
internal sealed record MsBuildDiagnostic(
    string Origin,
    int? Line,
    int? Column,
    string? Code,
    string Message,
    string? Project
);
