namespace Clicalo.App.SingleInstance;

/// <summary>
/// The server's half of the two-way check of the single-instance pipe (blueprint §3.4, ADR-0010): the client must be
/// in this session and run as this user (the DACL already keeps other users out; this does not rely on it alone), and
/// its integrity decides whether it may ask more than <c>show</c>. In M2 the only verb is <c>show</c>, so
/// <see cref="ClientTrust.ShowOnly"/> and <see cref="ClientTrust.Full"/> are served alike; the verbs of M4 read it.
/// </summary>
internal static class PipeAdmission
{
    /// <summary>What <paramref name="client"/> may ask the instance of <paramref name="identity"/>.</summary>
    /// <param name="client">The connected client.</param>
    /// <param name="identity">The names of this instance: its user and session.</param>
    /// <param name="serverIntegrity">The integrity level of this process, or <see langword="null"/> when unknown.</param>
    public static ClientTrust Of(
        PipeClient client,
        InstanceIdentity identity,
        uint? serverIntegrity
    )
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(identity);
        if (client.SessionId != (uint)identity.SessionId)
        {
            return ClientTrust.Rejected;
        }

        if (client.User is null || !client.User.Equals(identity.UserSid))
        {
            return ClientTrust.Rejected;
        }

        return
            client.IntegrityLevel is { } level && serverIntegrity is { } server && level >= server
            ? ClientTrust.Full
            : ClientTrust.ShowOnly;
    }
}
