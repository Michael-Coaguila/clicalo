using Clicalo.Application.Ports;
using Clicalo.Domain.Privacy;

namespace Clicalo.App.Composition;

/// <summary>
/// <see cref="IAiKeyStore"/> in memory, for a run with isolated data (<c>--data</c>): the key is forgotten when the
/// app exits and the Credential Manager is never touched.
/// </summary>
/// <param name="target">The target name the document keeps.</param>
internal sealed class MemoryKeyStore(string target) : IAiKeyStore
{
    private Sensitive<string>? _key;

    /// <inheritdoc />
    public string Target { get; } = target;

    /// <inheritdoc />
    public bool HasKey() => _key is not null;

    /// <inheritdoc />
    public bool Save(Sensitive<string> key)
    {
        _key = key;
        return true;
    }

    /// <inheritdoc />
    public Sensitive<string>? Read() => _key;

    /// <inheritdoc />
    public bool Delete()
    {
        _key = null;
        return true;
    }
}
