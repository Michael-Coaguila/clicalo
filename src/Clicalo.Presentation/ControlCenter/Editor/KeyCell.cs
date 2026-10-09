using Clicalo.Domain.Keys;

namespace Clicalo.Presentation.ControlCenter.Editor;

/// <summary>A key of the picker, or a modifier button (EDI-008).</summary>
/// <param name="Key">The catalog key.</param>
/// <param name="Label">Its name, in full.</param>
/// <param name="AccessibleName">Its name for screen readers.</param>
/// <param name="Chosen">Whether it is in the combination.</param>
public sealed record KeyCell(KeyId Key, string Label, string AccessibleName, bool Chosen);
