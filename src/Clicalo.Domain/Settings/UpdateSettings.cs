namespace Clicalo.Domain.Settings;

/// <summary>Updates (docs/02 <c>updates</c>, ACT-*).</summary>
/// <param name="Automatic">Check and download automatically.</param>
/// <param name="AskBefore">Ask before installing.</param>
/// <param name="BackupBefore">Back up before installing.</param>
/// <param name="Channel">Channel.</param>
public sealed record UpdateSettings(
    bool Automatic,
    bool AskBefore,
    bool BackupBefore,
    UpdateChannel Channel
);
