using Clicalo.Domain.Primitives;

namespace Clicalo.Application.UseCases.Editor;

/// <summary>
/// The question of ATJ-007 while it waits for an answer: «{proceso} ya abre «{p}». ¿Pasarlo a este perfil?»
/// ([processTaken]). Yes takes the process from its owner, which keeps its other processes; no leaves both as they are.
/// </summary>
/// <param name="Profile">The profile that wants the process.</param>
/// <param name="Process">The process.</param>
/// <param name="Owner">The profile that has it now.</param>
public sealed record TakeOverRequest(ProfileId Profile, ProcessName Process, ProfileId Owner);
