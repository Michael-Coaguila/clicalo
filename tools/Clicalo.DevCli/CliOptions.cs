namespace Clicalo.DevCli;

/// <summary>Options shared by the verbs; each verb accepts only the ones it documents.</summary>
internal sealed record CliOptions(string? Repo, bool Check, bool StrictUnused, string? Base);
