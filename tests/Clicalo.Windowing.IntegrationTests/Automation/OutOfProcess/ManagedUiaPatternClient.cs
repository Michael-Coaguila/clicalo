using System.Windows.Automation;

namespace Clicalo.Windowing.IntegrationTests.Automation.OutOfProcess;

/// <summary>The managed client of .NET (<see cref="UiaClientKind.Uia2"/>, <c>System.Windows.Automation</c>).</summary>
public sealed class ManagedUiaPatternClient(nint window) : IUiaPatternClient
{
    public void Run(string operation, string automationId)
    {
        var element = Find(automationId);
        switch (operation)
        {
            case UiaClientProcess.Invoke:
                ((InvokePattern)element.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
                break;
            case UiaClientProcess.Toggle:
                ((TogglePattern)element.GetCurrentPattern(TogglePattern.Pattern)).Toggle();
                break;
            case UiaClientProcess.Expand:
                (
                    (ExpandCollapsePattern)element.GetCurrentPattern(ExpandCollapsePattern.Pattern)
                ).Expand();
                break;
            case UiaClientProcess.Collapse:
                (
                    (ExpandCollapsePattern)element.GetCurrentPattern(ExpandCollapsePattern.Pattern)
                ).Collapse();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
        }
    }

    public void Dispose() { }

    private AutomationElement Find(string automationId) =>
        AutomationElement
            .FromHandle(window)
            .FindFirst(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.AutomationIdProperty, automationId)
            )
        ?? throw new InvalidOperationException(
            "UI Automation did not find " + automationId + " in the target window."
        );
}
