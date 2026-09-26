using System.Collections.Immutable;

namespace Clicalo.Domain.Messages;

/// <summary>What the build knows about a key of <c>data/i18n</c>; the generated <c>MessageCatalog</c> lists them all.</summary>
public sealed class MessageDescriptor
{
    /// <summary>Creates a descriptor.</summary>
    /// <param name="key">The key.</param>
    /// <param name="isPlural">True for a plural family selected by <c>{count}</c>.</param>
    /// <param name="parameters">Expected arguments, in declaration order.</param>
    public MessageDescriptor(
        MessageKey key,
        bool isPlural,
        params ReadOnlySpan<MessageParameter> parameters
    )
    {
        Key = key;
        IsPlural = isPlural;
        Parameters = [.. parameters];
    }

    /// <summary>The key.</summary>
    public MessageKey Key { get; }

    /// <summary>True for a plural family selected by <c>{count}</c>.</summary>
    public bool IsPlural { get; }

    /// <summary>Expected arguments, in <c>placeholders.json</c> declaration order.</summary>
    public ImmutableArray<MessageParameter> Parameters { get; }
}
