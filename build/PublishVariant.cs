namespace Clicalo.Build;

/// <summary>
/// One way of publishing <c>Clicalo.exe</c> that spike S5 compares (blueprint §11, §15.1): self-contained with
/// ReadyToRun, self-contained with composite ReadyToRun, and framework-dependent.
/// </summary>
/// <param name="Name">The folder and report name.</param>
/// <param name="SelfContained">Whether the .NET runtime ships with the app.</param>
/// <param name="ReadyToRun">Whether the assemblies are precompiled.</param>
/// <param name="Composite">Whether the precompiled code is one composite image (self-contained only).</param>
internal sealed record PublishVariant(
    string Name,
    bool SelfContained,
    bool ReadyToRun,
    bool Composite
)
{
    /// <summary>The variants of S5, the planned one first.</summary>
    public static IReadOnlyList<PublishVariant> All { get; } =
    [
        new("sc-r2r", SelfContained: true, ReadyToRun: true, Composite: false),
        new("sc-r2r-composite", SelfContained: true, ReadyToRun: true, Composite: true),
        new("fdd", SelfContained: false, ReadyToRun: false, Composite: false),
    ];

    /// <summary>The MSBuild properties of this variant.</summary>
    public IReadOnlyList<string> Properties =>
        [
            "--self-contained",
            SelfContained ? "true" : "false",
            "-p:PublishReadyToRun=" + (ReadyToRun ? "true" : "false"),
            "-p:PublishReadyToRunComposite=" + (Composite ? "true" : "false"),
        ];
}
