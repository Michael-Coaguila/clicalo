using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Migration.V1;

/// <summary>An imported profile before its id is assigned.</summary>
/// <param name="IsGeneral">Whether it becomes the General profile.</param>
/// <param name="IsCreated">Whether it did not come from v1 (General created empty, EC-MIG-04).</param>
/// <param name="Name">The v1 name, the same in every language; General takes the name of a new installation.</param>
/// <param name="Process">The process it keeps, in lower case, or <see langword="null"/> for a manual profile.</param>
/// <param name="Shortcuts">Its shortcuts, in v1 order, separators left out.</param>
internal sealed record V1ProfilePlan(
    bool IsGeneral,
    bool IsCreated,
    string Name,
    string? Process,
    ValueList<V1ShortcutPlan> Shortcuts
);
