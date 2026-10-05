namespace Clicalo.DevCli.I18n;

/// <summary>
/// A text placeholder that replaces a counted phrase of the handoff (<c>{p} perfiles</c>) with a nested plural
/// message (for example <c>itemsProfiles</c>) whose <c>{count}</c> takes the value of the original one-letter marker.
/// </summary>
internal sealed record HandoffComposedArgument(string Message, string CountMarker);
