namespace Clicalo.Application.Ports;

/// <summary>
/// «Iniciar con Windows» (SIS-002, ADR-0027): an entry of the user's <c>Run</c> key that starts the installed copy,
/// never elevated.
/// </summary>
public interface IStartupRegistration
{
    /// <summary>Whether this copy is the installed one; a development build never registers itself.</summary>
    bool IsAvailable { get; }

    /// <summary>Whether Windows starts Clícalo at sign-in.</summary>
    bool IsEnabled { get; }

    /// <summary>Adds or removes the entry; false when Windows refused or this copy is not the installed one.</summary>
    /// <param name="enabled">Whether Clícalo starts with Windows.</param>
    bool TrySetEnabled(bool enabled);
}
