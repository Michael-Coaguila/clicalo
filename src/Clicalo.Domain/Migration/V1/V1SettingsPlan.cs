using Clicalo.Domain.Settings;

namespace Clicalo.Domain.Migration.V1;

/// <summary>The settings v1 has an equivalent for (catalog §7.4, MIG-006); the rest keep the new installation's value.</summary>
/// <param name="Opacity">The opacity on the 0.05 grid.</param>
/// <param name="Size">The panel size of the v1 button height.</param>
/// <param name="LastProfile">Index in the planned profiles of <c>active_profile</c> (General when it is missing).</param>
/// <param name="Position">The panel position, or <see langword="null"/> to keep the default placement.</param>
internal sealed record V1SettingsPlan(
    double Opacity,
    PanelSize Size,
    int LastProfile,
    MonitorPosition? Position
);
