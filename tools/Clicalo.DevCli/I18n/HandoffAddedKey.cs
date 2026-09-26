using System.Collections.Immutable;

namespace Clicalo.DevCli.I18n;

/// <summary>A key that the handoff does not have and the reviewed conversion needs (an entry of <c>added</c>).</summary>
internal sealed record HandoffAddedKey(
    string Key,
    string After,
    ImmutableDictionary<string, ImmutableDictionary<string, string>> Plural
);
