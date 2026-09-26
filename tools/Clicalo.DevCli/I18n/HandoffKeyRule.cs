using System.Collections.Immutable;

namespace Clicalo.DevCli.I18n;

/// <summary>Reviewed conversion of one handoff key (an entry of <c>keys</c> in <c>handoff-import.json</c>).</summary>
internal sealed record HandoffKeyRule
{
    /// <summary>One-letter marker → placeholder name, overriding the default map for this key only.</summary>
    public ImmutableDictionary<string, string> Placeholders { get; init; } =
        ImmutableDictionary.Create<string, string>(StringComparer.Ordinal);

    /// <summary>Language → text that replaces the converted handoff text; empty when the text is converted.</summary>
    public ImmutableDictionary<string, string> Text { get; init; } =
        ImmutableDictionary.Create<string, string>(StringComparer.Ordinal);

    /// <summary>Language → plural category → text of the extra forms; the converted handoff text is <c>_other</c>.</summary>
    public ImmutableDictionary<string, ImmutableDictionary<string, string>> Plural { get; init; } =
        ImmutableDictionary.Create<string, ImmutableDictionary<string, string>>(
            StringComparer.Ordinal
        );

    /// <summary>Text placeholders filled with nested plural messages (only with <see cref="Text"/>).</summary>
    public ImmutableDictionary<string, HandoffComposedArgument> Arguments { get; init; } =
        ImmutableDictionary.Create<string, HandoffComposedArgument>(StringComparer.Ordinal);
}
