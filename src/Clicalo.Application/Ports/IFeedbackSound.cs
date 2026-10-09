namespace Clicalo.Application.Ports;

/// <summary>
/// The soft sound that confirms an action ran (EJE-012, GEN-011). Called from the engine thread: it returns at once
/// and plays elsewhere, so it never delays a send.
/// </summary>
public interface IFeedbackSound
{
    /// <summary>Plays the sound; a sound already playing is replaced.</summary>
    void Play();
}
