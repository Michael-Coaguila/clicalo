namespace Clicalo.Presentation.ControlCenter.General;

/// <summary>A row that is a switch as a whole (docs/07): icon, title, description and whether it is on.</summary>
/// <param name="Icon">Its Material Symbols icon.</param>
/// <param name="Title">Its title, also its accessible name.</param>
/// <param name="Description">The line under the title.</param>
/// <param name="On">Whether it is on.</param>
public sealed record SwitchItem(string Icon, string Title, string Description, bool On);
