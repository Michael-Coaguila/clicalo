namespace Clicalo.Application.Ports;

/// <summary>
/// Why a template generation gave no proposal (PLA-005, PLA-006). With the person's own key there is no daily quota
/// (user decision D5): the provider's own limit is reported as <see cref="BadKey"/>.
/// </summary>
public enum AiFailure
{
    /// <summary>It worked.</summary>
    None,

    /// <summary>No connection, or no answer within <c>Timings.Ai.AiRequestTimeout</c>.</summary>
    Offline,

    /// <summary>There is no key saved in the Credential Manager.</summary>
    NoKey,

    /// <summary>The provider refused the key, or its usage limit was reached.</summary>
    BadKey,

    /// <summary>The provider answered with an error of its own.</summary>
    Unavailable,

    /// <summary>The answer is not a valid proposal (structure or meaning).</summary>
    Invalid,
}
