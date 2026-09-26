namespace Clicalo.Build;

/// <summary>The first step that failed during a run and why.</summary>
/// <param name="Step">Short, dictable step name (for example <c>build</c>).</param>
/// <param name="Details">What went wrong.</param>
internal sealed record StepFailure(string Step, FailureDetails Details);
