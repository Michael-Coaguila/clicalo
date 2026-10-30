namespace Clicalo.App.Composition;

/// <summary>
/// What the surfaces show that a step of a guide may wait for (R-39), compared from one pass of the composition to the
/// next to tell what the person did.
/// </summary>
/// <param name="Frequents">Frequents is in view.</param>
/// <param name="PickerOpen">The profile grid is open.</param>
/// <param name="Page">The page of the panel.</param>
/// <param name="Minimized">The panel is the bubble.</param>
internal sealed record GuideWatch(bool Frequents, bool PickerOpen, int Page, bool Minimized);
