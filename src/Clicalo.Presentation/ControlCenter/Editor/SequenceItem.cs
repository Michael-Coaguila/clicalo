namespace Clicalo.Presentation.ControlCenter.Editor;

/// <summary>An item of the sequence of the «Probar» card: a key, a step or the action (PRB-001).</summary>
/// <param name="Label">What it shows.</param>
/// <param name="Icon">An icon before it, for steps and actions.</param>
/// <param name="Separator">«+» between keys or the arrow between steps, before it.</param>
/// <param name="Lit">Whether the animation has it pressed.</param>
public sealed record SequenceItem(string Label, string? Icon, string? Separator, bool Lit);
