using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.ProfileResolution;

namespace Clicalo.Application.UseCases;

/// <summary>What «Crear perfil» did (PER-009, PER-007).</summary>
/// <param name="Profile">The new profile, installed from the template with undo.</param>
/// <param name="Transition">
/// The profile state after the installation: the view moves to <paramref name="Profile"/> only in Auto and outside
/// Frequents (<see cref="ProfileResolver.OnTemplateInstalled"/>); the panel applies it to its session.
/// </param>
/// <param name="Notice">«Perfil instalado: {app}», shown with Undo.</param>
public sealed record SuggestionAccepted(
    ProfileId Profile,
    ProfileTransition Transition,
    Message Notice
);
