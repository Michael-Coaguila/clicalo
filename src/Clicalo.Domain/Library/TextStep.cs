using Clicalo.Domain.Privacy;

namespace Clicalo.Domain.Library;

/// <summary>Type a text as Unicode (EJE-008).</summary>
/// <param name="Text">The text.</param>
public sealed record TextStep(SecretText Text) : MacroStep;
