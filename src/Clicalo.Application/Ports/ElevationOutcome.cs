namespace Clicalo.Application.Ports;

/// <summary>What «Reabrir como administrador» did.</summary>
public enum ElevationOutcome
{
    /// <summary>The elevated instance started: this one must end now.</summary>
    Started,

    /// <summary>The person cancelled the UAC prompt, or there were no credentials.</summary>
    Cancelled,

    /// <summary>The running executable is not the installed one: nothing was started.</summary>
    NotInstalled,

    /// <summary>Windows could not start it.</summary>
    Failed,
}
