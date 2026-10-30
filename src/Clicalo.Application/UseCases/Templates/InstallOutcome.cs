using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;

namespace Clicalo.Application.UseCases.Templates;

/// <summary>What an install of Plantillas did (PLA-013, PLA-017).</summary>
/// <param name="Profile">The profile created or completed; the one to open for «Editar atajos».</param>
/// <param name="Notice">The message of the status bar, with [undo]; null when nothing changed.</param>
public sealed record InstallOutcome(ProfileId Profile, Message? Notice);
