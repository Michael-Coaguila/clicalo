namespace Clicalo.Domain.Execution;

/// <summary>Whether an app or web action may start (EJE-011, LOG-008).</summary>
public enum LaunchVerdict
{
    /// <summary>It may start.</summary>
    Allowed,

    /// <summary>A network path (UNC) of a shortcut that does not ask for confirmation.</summary>
    NetworkPath,

    /// <summary>It would run through a command interpreter or a script host.</summary>
    Interpreter,

    /// <summary>A web address that is not http or https, or a target that is not complete.</summary>
    Invalid,
}
