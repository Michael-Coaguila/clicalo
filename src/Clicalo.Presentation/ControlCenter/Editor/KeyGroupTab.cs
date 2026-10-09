using Clicalo.Domain.Keys;

namespace Clicalo.Presentation.ControlCenter.Editor;

/// <summary>A group of the key picker (EDI-008).</summary>
/// <param name="Group">The group.</param>
/// <param name="Label">Its name.</param>
/// <param name="Selected">Whether its keys show.</param>
public sealed record KeyGroupTab(KeyGroup Group, string Label, bool Selected);
