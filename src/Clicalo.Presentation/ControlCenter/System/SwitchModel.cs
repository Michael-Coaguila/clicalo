namespace Clicalo.Presentation.ControlCenter.SystemSection;

/// <summary>A row that is a switch as a whole (docs/07).</summary>
/// <param name="Icon">Its icon.</param>
/// <param name="Label">Its title.</param>
/// <param name="Description">Its description.</param>
/// <param name="On">Whether it is on.</param>
public sealed record SwitchModel(string Icon, string Label, string Description, bool On);
