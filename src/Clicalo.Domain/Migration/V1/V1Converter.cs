using Clicalo.Domain.Errors;

namespace Clicalo.Domain.Migration.V1;

/// <summary>
/// Pure, idempotent conversion of a v1 document into a Clícalo document (blueprint §6.6, catalog §7.4). A separate
/// stage, not a link of the migration chain. Repeated combinations it creates go to «It's fine» (MIG-008).
/// </summary>
/// <remarks>
/// Two halves: <see cref="V1Planner"/> decides everything from the v1 document (keys, targets, categories, settings and
/// the report) and <see cref="V1DocumentBuilder"/> builds the model with new ids. The same document and the same ids
/// always give the same result, and the v1 document is never changed.
/// </remarks>
public static class V1Converter
{
    /// <summary>Converts; a failure writes nothing and the welcome offers «Retry migration» (MIG-004).</summary>
    /// <param name="document">The v1 document.</param>
    /// <param name="context">Ids, defaults, monitors and clock.</param>
    public static Result<V1Conversion> Convert(V1Document document, V1ConversionContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(context);
        return V1DocumentBuilder.Build(V1Planner.Plan(document, context.Monitors), context);
    }
}
