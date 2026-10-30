using Clicalo.Domain.Library;

namespace Clicalo.Domain.Sharing;

/// <summary>
/// A shared profile (<c>clicalo-perfil-&lt;id&gt;.json</c>, DAT-007) read for the preview of Plantillas: nothing is
/// installed or run yet (LOG-006).
/// </summary>
/// <param name="Profile">The profile, with new ids for itself and every shortcut (DAT-004).</param>
/// <param name="UnavailableTexts">Texts left out when it was shared: those shortcuts are incomplete (COP-005).</param>
public sealed record SharedProfile(Profile Profile, int UnavailableTexts);
