namespace Clicalo.DevCli.Adr;

/// <summary>One entry of <c>architecture/sensitive-paths.json</c>: a glob whose change needs an ADR.</summary>
internal sealed record SensitivePath(PathGlob Glob, string Category, string Reason);
