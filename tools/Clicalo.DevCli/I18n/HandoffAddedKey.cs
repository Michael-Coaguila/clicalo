using System.Collections.Immutable;

namespace Clicalo.DevCli.I18n;

/// <summary>
/// A key that the handoff does not have and the reviewed conversion needs (an entry of <c>added</c>). It has either a
/// <see cref="Text"/> per language or a <see cref="Plural"/> family per language, never both.
/// </summary>
internal sealed record HandoffAddedKey(
    string Key,
    string After,
    ImmutableDictionary<string, string> Text,
    ImmutableDictionary<string, ImmutableDictionary<string, string>> Plural
);
