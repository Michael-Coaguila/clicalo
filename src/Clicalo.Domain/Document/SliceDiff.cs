namespace Clicalo.Domain.Document;

/// <summary>Which slices a change touched, compared by reference (blueprint §6.4).</summary>
/// <remarks>
/// Commands and aggregate operations return the same instance for a part they do not change, so reference comparison
/// is exact and costs nothing. The curation compares the arrays behind <c>Pins</c> and <c>Hidden</c>; the usage slice
/// compares the history and the usage epoch.
/// </remarks>
public static class SliceDiff
{
    /// <summary>The slices whose reference differs between the two documents; the revision alone is not a slice.</summary>
    /// <param name="before">Document before the change.</param>
    /// <param name="after">Document after the change.</param>
    public static DocumentSlices Touched(UserDocument before, UserDocument after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        var slices = DocumentSlices.None;
        if (!ReferenceEquals(before.Library, after.Library))
        {
            slices |= DocumentSlices.Library;
        }

        var frequentsBefore = before.Frequents;
        var frequentsAfter = after.Frequents;
        if (!ReferenceEquals(frequentsBefore, frequentsAfter))
        {
            if (
                frequentsBefore.Pins.Items != frequentsAfter.Pins.Items
                || frequentsBefore.Hidden.Items != frequentsAfter.Hidden.Items
            )
            {
                slices |= DocumentSlices.FrequentsCuration;
            }

            if (
                !ReferenceEquals(frequentsBefore.Usage, frequentsAfter.Usage)
                || frequentsBefore.UsageEpoch != frequentsAfter.UsageEpoch
            )
            {
                slices |= DocumentSlices.FrequentsUsage;
            }
        }

        if (!ReferenceEquals(before.Duplicates, after.Duplicates))
        {
            slices |= DocumentSlices.Duplicates;
        }

        if (!ReferenceEquals(before.Settings, after.Settings))
        {
            slices |= DocumentSlices.Settings;
        }

        if (!ReferenceEquals(before.Onboarding, after.Onboarding))
        {
            slices |= DocumentSlices.Onboarding;
        }

        return slices;
    }
}
