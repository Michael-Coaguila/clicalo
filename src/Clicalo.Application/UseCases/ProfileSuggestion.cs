using Clicalo.Domain.Primitives;
using Clicalo.Domain.Templates;

namespace Clicalo.Application.UseCases;

/// <summary>
/// The suggestion card of the panel (PER-009, docs/04 §6): «<b>{app}</b> no tiene perfil. ¿Creo uno con atajos
/// listos?», offered for the app in front because a template binds its process.
/// </summary>
/// <param name="Process">The executable of the app in front; «Ahora no» dismisses it for the session.</param>
/// <param name="Template">The template that «Crear perfil» installs; its name is the app name the card shows.</param>
public sealed record ProfileSuggestion(ProcessName Process, ProfileTemplate Template);
