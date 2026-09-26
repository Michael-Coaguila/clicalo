using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Migration.V1;

/// <summary>
/// A Macro Quick Access <c>profiles.json</c> as read (catalog §7.2, MIG-002): every key optional, unknown keys kept by
/// name for the report, missing values left <see langword="null"/> so the converter applies the v1 defaults.
/// </summary>
/// <param name="ActiveProfile"><c>active_profile</c>.</param>
/// <param name="PinnedProfile"><c>pinned_profile</c>.</param>
/// <param name="WindowPosition"><c>window_pos</c>, logical Qt pixels on the virtual desktop.</param>
/// <param name="WindowSize"><c>window_size</c>.</param>
/// <param name="EditSize"><c>edit_size</c>.</param>
/// <param name="WindowOpacity"><c>window_opacity</c>.</param>
/// <param name="ButtonSize"><c>button_size</c>.</param>
/// <param name="Profiles">The profiles in file order (the display order).</param>
/// <param name="UnknownKeys">Top-level keys the schema does not know (for example <c>_nota</c>).</param>
public sealed record V1Document(
    string? ActiveProfile,
    string? PinnedProfile,
    V1Pair? WindowPosition,
    V1Pair? WindowSize,
    V1Pair? EditSize,
    double? WindowOpacity,
    V1Pair? ButtonSize,
    ValueList<V1Profile> Profiles,
    ValueList<string> UnknownKeys
);
