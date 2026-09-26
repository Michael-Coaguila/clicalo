namespace Clicalo.Build;

/// <summary>A dependency, tool, SDK or action that is not pinned the way blueprint §2.1 and §10.5 require.</summary>
/// <param name="File">Path relative to the repository root, with forward slashes.</param>
/// <param name="Line">1-based line, when known.</param>
/// <param name="Subject">What is affected (package id, tool, action reference).</param>
/// <param name="Problem">Why it is rejected, in plain words.</param>
internal sealed record VersionPinViolation(string File, int? Line, string Subject, string Problem);
