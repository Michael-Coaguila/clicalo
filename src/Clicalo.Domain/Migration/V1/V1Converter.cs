using System.Diagnostics.CodeAnalysis;
using Clicalo.Domain.Errors;

namespace Clicalo.Domain.Migration.V1;

/// <summary>
/// Pure, idempotent conversion of a v1 document into a Clícalo document (blueprint §6.6, catalog §7.4). A separate
/// stage, not a link of the migration chain. Repeated combinations it creates go to «It's fine» (MIG-008).
/// </summary>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality instead of throwing NotImplementedException",
    Justification = "M2 contract; the migration package implements it (docs/testing/spikes/M2-ownership.md)."
)]
public static class V1Converter
{
    /// <summary>Converts; a failure writes nothing and the welcome offers «Retry migration» (MIG-004).</summary>
    /// <param name="document">The v1 document.</param>
    /// <param name="context">Ids, defaults, monitors and clock.</param>
    public static Result<V1Conversion> Convert(V1Document document, V1ConversionContext context) =>
        throw new NotImplementedException();
}
