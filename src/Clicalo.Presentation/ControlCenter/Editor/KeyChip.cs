namespace Clicalo.Presentation.ControlCenter.Editor;

/// <summary>A key of the combination box, in press order, with its remove button (EDI-007).</summary>
/// <param name="Index">Its zero-based position.</param>
/// <param name="Label">Its name.</param>
/// <param name="RemoveName">[removeKeyN].</param>
public sealed record KeyChip(int Index, string Label, string RemoveName);
