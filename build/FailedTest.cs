namespace Clicalo.Build;

/// <summary>A test that did not pass, as read from a TRX report.</summary>
/// <param name="Name">Fully qualified test name.</param>
/// <param name="Outcome">TRX outcome (<c>Failed</c>, <c>Error</c>, <c>Timeout</c> or <c>Aborted</c>).</param>
/// <param name="Message">Assertion or exception message, when present.</param>
/// <param name="StackTrace">Stack trace, when present.</param>
internal sealed record FailedTest(string Name, string Outcome, string? Message, string? StackTrace);
