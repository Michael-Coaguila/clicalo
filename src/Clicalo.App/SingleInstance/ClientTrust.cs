namespace Clicalo.App.SingleInstance;

/// <summary>What a client of the single-instance pipe may ask (blueprint §3.4, ADR-0010).</summary>
internal enum ClientTrust
{
    /// <summary>Another session, another user, or a client whose user could not be read: nothing is served.</summary>
    Rejected,

    /// <summary>The same user with less integrity than the server (or an unreadable level): only <c>show</c>.</summary>
    ShowOnly,

    /// <summary>The same user with at least the server's integrity: every verb (M2 has only <c>show</c>).</summary>
    Full,
}
