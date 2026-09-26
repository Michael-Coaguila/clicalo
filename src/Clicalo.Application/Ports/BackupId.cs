namespace Clicalo.Application.Ports;

/// <summary>Identity of a backup (its file name).</summary>
/// <param name="Value">The identifier.</param>
public readonly record struct BackupId(string Value);
