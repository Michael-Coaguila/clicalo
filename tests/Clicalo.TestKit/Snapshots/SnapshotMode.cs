namespace Clicalo.TestKit.Snapshots;

/// <summary>What a snapshot assertion does when the received value differs from the verified file.</summary>
public enum SnapshotMode
{
    /// <summary>Fail, and write the received value next to the verified file for review.</summary>
    Verify,

    /// <summary>Overwrite the verified file with the received value and pass (<c>CLICALO_ACCEPT_SNAPSHOTS=1</c>).</summary>
    Accept,
}
