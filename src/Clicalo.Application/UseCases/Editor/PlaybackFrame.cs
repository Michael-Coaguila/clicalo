namespace Clicalo.Application.UseCases.Editor;

/// <summary>One moment of the «Ver» animation of the «Probar» card (PRB-002).</summary>
/// <param name="At">When it starts, from the tap on «Ver».</param>
/// <param name="Down">How many items of the sequence are lit (keys pressed, or the steps up to the current one).</param>
/// <param name="Phase">The phase line.</param>
/// <param name="Step">The zero-based step of a macro in <see cref="PlaybackPhase.Step"/>; -1 otherwise.</param>
public sealed record PlaybackFrame(TimeSpan At, int Down, PlaybackPhase Phase, int Step);
