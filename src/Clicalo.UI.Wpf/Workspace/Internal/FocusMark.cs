namespace Clicalo.UI.Wpf.Workspace.Internal;

/// <summary>What <see cref="FocusKeeper"/> remembers of the control that had the keyboard (ACC-004).</summary>
/// <param name="Kind">The type of the control.</param>
/// <param name="Name">Its accessible name.</param>
/// <param name="Ordinal">Its place among the focusable controls of the window.</param>
internal sealed record FocusMark(Type Kind, string Name, int Ordinal);
