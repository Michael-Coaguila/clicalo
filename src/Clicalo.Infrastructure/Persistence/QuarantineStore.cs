using System.Diagnostics.CodeAnalysis;
using Clicalo.Domain.Errors;

namespace Clicalo.Infrastructure.Persistence;

/// <summary>
/// Moves an unreadable document to <c>quarantine\clicalo.&lt;date&gt;.json.corrupt</c> (blueprint §6.5, DAT-003,
/// REG-08): never deletes it and never overwrites an earlier quarantined file.
/// </summary>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality instead of throwing NotImplementedException",
    Justification = "M2 contract; the persistence package implements it (docs/testing/spikes/M2-ownership.md)."
)]
public sealed class QuarantineStore
{
    /// <summary>Creates the store.</summary>
    /// <param name="locations">Where the data lives.</param>
    /// <param name="time">Clock of the file names.</param>
    public QuarantineStore(DataLocations locations, TimeProvider time) =>
        throw new NotImplementedException();

    /// <summary>Moves <paramref name="path"/> into quarantine and returns the new path.</summary>
    /// <param name="path">The unreadable file.</param>
    public Result<string> Move(string path) => throw new NotImplementedException();
}
