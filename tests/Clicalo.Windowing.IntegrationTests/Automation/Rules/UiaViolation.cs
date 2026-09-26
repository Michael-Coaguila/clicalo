namespace Clicalo.Windowing.IntegrationTests.Automation.Rules;

/// <summary>One broken UIA rule on one element.</summary>
/// <param name="RuleId">UIA001…UIA010.</param>
/// <param name="Element">AutomationId (or name) of the element.</param>
/// <param name="Message">What is wrong.</param>
public sealed record UiaViolation(string RuleId, string Element, string Message)
{
    /// <summary>One line for failure messages.</summary>
    public override string ToString() => $"{RuleId} [{Element}] {Message}";
}
