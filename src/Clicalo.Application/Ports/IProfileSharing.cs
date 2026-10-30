using Clicalo.Domain.Errors;
using Clicalo.Domain.Library;
using Clicalo.Domain.Sharing;

namespace Clicalo.Application.Ports;

/// <summary>
/// Shares one profile as <c>clicalo-perfil-&lt;id&gt;.json</c> with <c>"type":"profile-share"</c> (DAT-007), and reads
/// one back as untrusted content (LOG-006): limits, a strict format and new ids, nothing installed or run.
/// </summary>
public interface IProfileSharing
{
    /// <summary>The file of <paramref name="profile"/>.</summary>
    /// <param name="profile">The profile.</param>
    /// <param name="includeTextsInClear">The person chose to include the texts in clear; otherwise they are left out.</param>
    SharedProfileFile Export(Profile profile, bool includeTextsInClear);

    /// <summary>Reads a shared profile for the preview.</summary>
    /// <param name="utf8">The file bytes.</param>
    Result<SharedProfile> Import(ReadOnlyMemory<byte> utf8);
}
