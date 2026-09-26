namespace Clicalo.DevCli.Adr;

/// <summary>A changed file that matches a sensitive path.</summary>
internal sealed record AdrTouch(string File, SensitivePath Path);
