namespace Clicalo.Windowing.IntegrationTests.Automation.OutOfProcess;

/// <summary>A UI Automation client of the helper process: it calls one pattern method on an element of the target window.</summary>
public interface IUiaPatternClient : IDisposable
{
    /// <summary>
    /// Finds the element with <paramref name="automationId"/> in the target window and calls
    /// <paramref name="operation"/> on it: <c>invoke</c>, <c>toggle</c>, <c>expand</c> or <c>collapse</c>.
    /// </summary>
    void Run(string operation, string automationId);
}
