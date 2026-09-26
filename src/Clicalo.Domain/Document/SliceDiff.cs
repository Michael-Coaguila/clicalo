using System.Diagnostics.CodeAnalysis;

namespace Clicalo.Domain.Document;

/// <summary>Which slices a change touched, compared by reference (blueprint §6.4).</summary>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality instead of throwing NotImplementedException",
    Justification = "M2 contract; the domain package implements it (docs/testing/spikes/M2-ownership.md)."
)]
public static class SliceDiff
{
    /// <summary>The slices whose reference differs between the two documents; the revision alone is not a slice.</summary>
    /// <param name="before">Document before the change.</param>
    /// <param name="after">Document after the change.</param>
    public static DocumentSlices Touched(UserDocument before, UserDocument after) =>
        throw new NotImplementedException();
}
