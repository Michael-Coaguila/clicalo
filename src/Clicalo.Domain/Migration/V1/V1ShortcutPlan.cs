using Clicalo.Domain.Catalog;

namespace Clicalo.Domain.Migration.V1;

/// <summary>An imported shortcut before its id is assigned.</summary>
/// <param name="Name">The v1 label, the same in every language (catalog §7.4).</param>
/// <param name="Category">The category of its colour (PQ-08).</param>
/// <param name="Action">What it does.</param>
internal sealed record V1ShortcutPlan(string Name, CategoryId Category, V1ActionPlan Action);
