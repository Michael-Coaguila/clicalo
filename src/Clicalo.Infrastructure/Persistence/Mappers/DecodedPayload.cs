using System.Collections.Immutable;
using Clicalo.Domain.Document;

namespace Clicalo.Infrastructure.Persistence.Mappers;

/// <summary>A payload read into the Domain.</summary>
/// <param name="Document">The valid document (revision 0).</param>
/// <param name="Preserved">What a rewrite must keep.</param>
/// <param name="Repairs">The stable codes of what was repaired (empty when nothing was).</param>
/// <param name="UnavailableTexts">Texts that cannot be decrypted here (COP-005).</param>
internal sealed record DecodedPayload(
    UserDocument Document,
    PreservedFields Preserved,
    ImmutableArray<string> Repairs,
    int UnavailableTexts
);
