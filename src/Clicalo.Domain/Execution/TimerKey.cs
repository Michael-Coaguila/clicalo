namespace Clicalo.Domain.Execution;

/// <summary>Identity of an engine timer (deadline, macro wait, scroll repeat, confirmation, delayed Shift).</summary>
/// <param name="Value">Stable text, such as <c>deadline</c> or <c>macro:12</c>.</param>
public readonly record struct TimerKey(string Value);
