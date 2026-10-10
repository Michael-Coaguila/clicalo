namespace Clicalo.DevCli.Trace;

/// <summary>A test, or a test class, that declares <c>Trait("Req", Id)</c>.</summary>
/// <param name="Id">The requirement the trait names.</param>
/// <param name="File">The source file, relative to the repository root, with forward slashes.</param>
/// <param name="Line">The 1-based line of the trait.</param>
/// <param name="Test"><c>Class.Method</c>, or <c>Class</c> when the trait is on the class and covers all its tests.</param>
internal sealed record TestReference(string Id, string File, int Line, string Test);
