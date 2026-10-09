namespace Clicalo.Application.UseCases;

/// <summary>What happened when a search result was run (BUS-002 c and d).</summary>
public enum SearchRunOutcome
{
    /// <summary>The foreground was back on the app (verified) and the activation went to the engine.</summary>
    Sent,

    /// <summary>The foreground could not be given back to the app: nothing was sent, and the user is told.</summary>
    NotSent,

    /// <summary>The engine has stopped (Clícalo is exiting); nothing was sent.</summary>
    EngineStopped,
}
