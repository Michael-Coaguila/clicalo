using System.Collections.Frozen;
using Clicalo.Application.Ports;
using Clicalo.Domain.Privacy;
using Clicalo.Domain.Settings;

namespace Clicalo.Application.UseCases.Ai;

/// <summary>
/// The key of the provider chosen in the document (ADR-0033): every provider has its own key in the Credential Manager
/// (<c>Clicalo/ai/{provider}</c>), and pasting, reading or deleting always works on the chosen one, so a key saved for
/// one provider is never sent to another.
/// </summary>
public sealed class ChosenProviderKeyStore : IAiKeyStore
{
    private readonly Func<AiProvider> _chosen;
    private readonly FrozenDictionary<AiProvider, IAiKeyStore> _stores;

    /// <summary>Creates the store.</summary>
    /// <param name="chosen">The provider in the document.</param>
    /// <param name="storeOf">The key store of a provider, asked once for each.</param>
    public ChosenProviderKeyStore(Func<AiProvider> chosen, Func<AiProvider, IAiKeyStore> storeOf)
    {
        ArgumentNullException.ThrowIfNull(chosen);
        ArgumentNullException.ThrowIfNull(storeOf);
        _chosen = chosen;
        _stores = Enum.GetValues<AiProvider>().ToFrozenDictionary(static p => p, storeOf);
    }

    /// <inheritdoc />
    public string Target => Current.Target;

    private IAiKeyStore Current => _stores[_chosen()];

    /// <inheritdoc />
    public bool HasKey() => Current.HasKey();

    /// <inheritdoc />
    public bool Save(Sensitive<string> key) => Current.Save(key);

    /// <inheritdoc />
    public Sensitive<string>? Read() => Current.Read();

    /// <inheritdoc />
    public bool Delete() => Current.Delete();
}
