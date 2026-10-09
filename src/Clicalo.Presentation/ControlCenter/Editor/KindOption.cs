using Clicalo.Domain.Library;

namespace Clicalo.Presentation.ControlCenter.Editor;

/// <summary>A tile of the «Qué hace» grid (EDI-006).</summary>
/// <param name="Kind">The kind.</param>
/// <param name="Icon">Its icon.</param>
/// <param name="Label">Its name.</param>
/// <param name="Selected">Whether it is the kind of the shortcut.</param>
public sealed record KindOption(ActionKind Kind, string Icon, string Label, bool Selected);
