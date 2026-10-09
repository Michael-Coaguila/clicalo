using Clicalo.Domain.Privacy;

namespace Clicalo.Application.Ports;

/// <summary>
/// The person's own AI key in the Windows Credential Manager (ADR-0008, PLA-003, user decision D5): it can be pasted
/// or deleted, never shown again, and the document only keeps <see cref="Target"/>.
/// </summary>
public interface IAiKeyStore
{
    /// <summary>The Credential Manager target, such as <c>Clicalo/ai/anthropic</c>: the reference the document keeps.</summary>
    string Target { get; }

    /// <summary>Whether a key is saved.</summary>
    bool HasKey();

    /// <summary>Saves the key, replacing the one there was.</summary>
    /// <param name="key">The key.</param>
    /// <returns>Whether it was saved.</returns>
    bool Save(Sensitive<string> key);

    /// <summary>Reads the key for a request; null without one.</summary>
    Sensitive<string>? Read();

    /// <summary>Deletes the key.</summary>
    /// <returns>Whether there is no key left.</returns>
    bool Delete();
}
