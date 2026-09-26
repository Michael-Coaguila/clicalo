namespace Clicalo.Windowing.IntegrationTests.Automation.OutOfProcess;

/// <summary>Which UI Automation client the helper process uses.</summary>
public enum UiaClientKind
{
    /// <summary>
    /// The COM client of UIAutomationCore through FlaUI.UIA3 (<c>IUIAutomation</c>), as Voice access, Narrator and
    /// Axe.Windows use it.
    /// </summary>
    Uia3,

    /// <summary>
    /// The managed client of .NET (<c>System.Windows.Automation</c>, UIAutomationClient), which older assistive tools
    /// and scripts use (S3: one of them activated a SpikeLab surface).
    /// </summary>
    Uia2,
}
