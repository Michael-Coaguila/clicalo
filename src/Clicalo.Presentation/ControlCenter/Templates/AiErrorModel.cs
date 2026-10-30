using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.ControlCenter.Templates;

/// <summary>The error card of «Crear con IA» (PLA-006): announced assertively.</summary>
/// <param name="Icon">Its icon.</param>
/// <param name="Title">Its title.</param>
/// <param name="Text">Its description.</param>
/// <param name="Actions">The three ways out.</param>
public sealed record AiErrorModel(
    string Icon,
    string Title,
    string Text,
    ValueList<ErrorAction> Actions
);
