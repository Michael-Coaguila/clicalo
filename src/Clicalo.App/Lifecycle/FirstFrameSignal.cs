namespace Clicalo.App.Lifecycle;

/// <summary>
/// Tells a measuring process that the first frame of the panel is on screen (spike S5, NFR-001): when the environment
/// variable <see cref="Variable"/> names an event the measurer created, it is set once. Nothing happens in a normal
/// start. The measurer takes the process creation time as the start, so the number includes everything the process
/// does before its first frame.
/// </summary>
internal static class FirstFrameSignal
{
    /// <summary>The environment variable with the name of the event.</summary>
    public const string Variable = "CLICALO_FIRST_FRAME_EVENT";

    /// <summary>Sets the event, if a measurer asked for it.</summary>
    public static void Raise()
    {
        var name = Environment.GetEnvironmentVariable(Variable);
        if (
            string.IsNullOrWhiteSpace(name)
            || !EventWaitHandle.TryOpenExisting(name, out var handle)
        )
        {
            return;
        }

        using (handle)
        {
            _ = handle.Set();
        }
    }
}
