namespace Clicalo.Application.UseCases.Welcome;

/// <summary>The welcome ended.</summary>
/// <param name="end">How.</param>
public sealed class WelcomeEndedEventArgs(WelcomeEnd end) : EventArgs
{
    /// <summary>How it ended.</summary>
    public WelcomeEnd End { get; } = end;
}
