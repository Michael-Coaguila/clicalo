using System.Collections.Immutable;

namespace Clicalo.Generators.Localization;

/// <summary>A logical key (a plain text or a plural family) that becomes one generated member.</summary>
internal sealed class MessageDefinition(
    string key,
    string memberName,
    bool isPlural,
    ImmutableArray<PlaceholderDefinition> arguments,
    ImmutableArray<MessageForm> defaultForms
)
{
    /// <summary>Base key, identical to the original handoff key (<c>dupHead</c>).</summary>
    public string Key { get; } = key;

    /// <summary>PascalCase member name (<c>DupHead</c>).</summary>
    public string MemberName { get; } = memberName;

    /// <summary>True for a plural family selected by <c>{count}</c>.</summary>
    public bool IsPlural { get; } = isPlural;

    /// <summary>Arguments in <c>placeholders.json</c> declaration order.</summary>
    public ImmutableArray<PlaceholderDefinition> Arguments { get; } = arguments;

    /// <summary>Texts of the default language, in canonical category order.</summary>
    public ImmutableArray<MessageForm> DefaultForms { get; } = defaultForms;
}
