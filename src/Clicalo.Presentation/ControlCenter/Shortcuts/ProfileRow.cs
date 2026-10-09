using Clicalo.Domain.Library;

namespace Clicalo.Presentation.ControlCenter.Shortcuts;

/// <summary>A row of the profiles column (ATJ-002).</summary>
/// <param name="List">The list it opens.</param>
/// <param name="Icon">Its icon.</param>
/// <param name="Name">Its name.</param>
/// <param name="Sub">[allApps], its processes or [noProcess], in monospace.</param>
/// <param name="Selected">Whether it is the list in view (cardHi and the selected state).</param>
public sealed record ProfileRow(ListRef List, string Icon, string Name, string Sub, bool Selected);
