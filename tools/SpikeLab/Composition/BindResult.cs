namespace Clicalo.Tools.SpikeLab.Composition;

/// <summary>The outcome of <see cref="ConstructorBinder.Create"/>.</summary>
/// <param name="Instance">The created object, or null.</param>
/// <param name="Problem">Why it could not be created, or null.</param>
internal sealed record BindResult(object? Instance, string? Problem)
{
    /// <summary>A created object.</summary>
    public static BindResult Created(object instance) => new(instance, null);

    /// <summary>Nothing was created because of <paramref name="problem"/>.</summary>
    public static BindResult Missing(string problem) => new(null, problem);
}
