using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace Clicalo.Windowing.IntegrationTests.Automation.OutOfProcess;

/// <summary>The UIA3 client (<see cref="UiaClientKind.Uia3"/>), through FlaUI.</summary>
public sealed class FlaUiPatternClient(nint window) : IUiaPatternClient
{
    private readonly UIA3Automation _automation = new();

    public void Run(string operation, string automationId)
    {
        var element = Find(automationId);
        switch (operation)
        {
            case UiaClientProcess.Invoke:
                element.Patterns.Invoke.Pattern.Invoke();
                break;
            case UiaClientProcess.Toggle:
                element.Patterns.Toggle.Pattern.Toggle();
                break;
            case UiaClientProcess.Expand:
                element.Patterns.ExpandCollapse.Pattern.Expand();
                break;
            case UiaClientProcess.Collapse:
                element.Patterns.ExpandCollapse.Pattern.Collapse();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
        }
    }

    public void Dispose() => _automation.Dispose();

    private AutomationElement Find(string automationId) =>
        _automation
            .FromHandle(window)
            .FindFirstDescendant(condition => condition.ByAutomationId(automationId))
        ?? throw new InvalidOperationException(
            "UI Automation did not find " + automationId + " in the target window."
        );
}
