using Clicalo.Domain.Library;

namespace Clicalo.Domain.Frequents;

/// <summary>One tile of the Frequents view (FRE-001, FRE-003).</summary>
/// <param name="Shortcut">The shortcut; it runs with the profile of its list (FRE-003).</param>
/// <param name="Location">Where it lives, shown under its name as its origin.</param>
/// <param name="Pinned">Whether it is pinned (the 📌 badge replaces the type badge).</param>
/// <param name="Uses">Its executions within the usage window.</param>
public sealed record FrequentEntry(
    Shortcut Shortcut,
    ShortcutLocation Location,
    bool Pinned,
    int Uses
);
