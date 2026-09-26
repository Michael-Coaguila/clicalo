namespace Clicalo.TestKit.Snapshots;

/// <summary>Process-wide snapshot configuration, read from the environment.</summary>
public static class SnapshotSettings
{
    /// <summary>Set to <c>1</c> (or <c>true</c>) to accept every received snapshot as the new verified file.</summary>
    public const string AcceptVariable = "CLICALO_ACCEPT_SNAPSHOTS";

    /// <summary>
    /// The mode requested by <see cref="AcceptVariable"/>. Accepting is refused on CI (<c>CI</c> or
    /// <c>GITHUB_ACTIONS</c> set to <c>true</c>): a pipeline must never approve its own output.
    /// </summary>
    public static SnapshotMode CurrentMode
    {
        get
        {
            if (!IsTrue(Environment.GetEnvironmentVariable(AcceptVariable)))
            {
                return SnapshotMode.Verify;
            }

            if (
                IsTrue(Environment.GetEnvironmentVariable("CI"))
                || IsTrue(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"))
            )
            {
                throw new InvalidOperationException(
                    AcceptVariable
                        + " is set on a CI run. Snapshots are accepted locally and reviewed in the pull request."
                );
            }

            return SnapshotMode.Accept;
        }
    }

    private static bool IsTrue(string? value) =>
        string.Equals(value, "1", StringComparison.Ordinal)
        || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
}
