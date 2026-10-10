namespace Clicalo.Application.UseCases.Welcome;

/// <summary>How the welcome ended.</summary>
public enum WelcomeEnd
{
    /// <summary>[Empezar] on the last step (BIE-009): the panel is shown with the notice [welcome].</summary>
    Finished,

    /// <summary>[Omitir] or Alt+F4 (BIE-003): it closes keeping what was applied.</summary>
    Skipped,
}
