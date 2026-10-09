using System.Collections.Frozen;

namespace Clicalo.Domain.Keys;

/// <summary>
/// The labels of the catalog keys by canonical id, read from <c>data/catalogs/keys.json</c> at run time (R-04). Sided
/// keys keep their own entry (<c>rctrl</c> «Ctrl der.»); <see cref="KeyChordFormatter"/> finds it from a stroke's base
/// key and side.
/// </summary>
public sealed class KeyLabelCatalog
{
    private readonly FrozenDictionary<KeyId, KeyLabel> _labels;

    /// <summary>Creates the catalog; a key that repeats keeps its first labels.</summary>
    /// <param name="labels">The labels by key.</param>
    public KeyLabelCatalog(IEnumerable<KeyValuePair<KeyId, KeyLabel>> labels)
    {
        ArgumentNullException.ThrowIfNull(labels);
        var byKey = new Dictionary<KeyId, KeyLabel>();
        foreach (var (key, label) in labels)
        {
            if (label is not null)
            {
                byKey.TryAdd(key, label);
            }
        }

        _labels = byKey.ToFrozenDictionary();
    }

    /// <summary>A catalog without labels: keys are written by their id (a character key by its character).</summary>
    public static KeyLabelCatalog Empty { get; } = new([]);

    /// <summary>How many keys have labels.</summary>
    public int Count => _labels.Count;

    /// <summary>The labels of <paramref name="key"/>.</summary>
    /// <param name="key">A canonical key id.</param>
    /// <param name="label">Its labels.</param>
    public bool TryGet(KeyId key, out KeyLabel label) => _labels.TryGetValue(key, out label!);
}
