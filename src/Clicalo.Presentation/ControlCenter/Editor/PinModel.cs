namespace Clicalo.Presentation.ControlCenter.Editor;

/// <summary>The row «Fijar en Siempre visible» (EDI-015).</summary>
/// <param name="Title">[pinAll2].</param>
/// <param name="Description">[pinAllOn2] or [pinAllOff2].</param>
/// <param name="On">The switch.</param>
public sealed record PinModel(string Title, string Description, bool On);
