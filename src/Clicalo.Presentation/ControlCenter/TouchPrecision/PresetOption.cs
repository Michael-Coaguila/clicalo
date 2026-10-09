namespace Clicalo.Presentation.ControlCenter.TouchPrecision;

/// <summary>A preset card (TAC-001, TAC-005): its name and description, and whether it is the one in use.</summary>
/// <param name="Id">The preset id, or <c>personal</c>.</param>
/// <param name="Label">[pStd], [pLeve], [pFuerte] or [pCustom].</param>
/// <param name="Description">[dStd], [dLeve], [dFuerte] or [dCustom].</param>
/// <param name="Selected">Whether it is in use: accent outline and the Toggle state, not only color.</param>
public sealed record PresetOption(string Id, string Label, string Description, bool Selected);
