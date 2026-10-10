namespace Clicalo.Application.Ports;

/// <summary>Why an update failed (ACT-001: new error texts).</summary>
public enum UpdateError
{
    /// <summary>No error.</summary>
    None,

    /// <summary>No connection, or the channel did not answer.</summary>
    Offline,

    /// <summary>The package did not match its checksum (the verification of Velopack).</summary>
    Damaged,

    /// <summary>No room on the disk.</summary>
    NoSpace,

    /// <summary>The download or the installation stopped half-way.</summary>
    Interrupted,
}
