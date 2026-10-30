using Clicalo.Domain.Keys;

namespace Clicalo.Domain.Settings;

/// <summary>
/// One combination of the closed list of the global shortcut (BUR-005, <c>data/catalogs/global-hotkeys.json</c>).
/// </summary>
/// <param name="Id">The persisted id (<c>ctrl-alt-space</c>); stable, never renamed.</param>
/// <param name="Keys">The combination, in press order.</param>
public sealed record GlobalHotkey(string Id, KeyChord Keys);
