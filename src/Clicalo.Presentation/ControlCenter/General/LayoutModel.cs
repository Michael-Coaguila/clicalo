using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.ControlCenter.General;

/// <summary>«Disposición» (GEN-006 to GEN-008).</summary>
/// <param name="Caption">[secLayout].</param>
/// <param name="RowsTitle">[rowsVis].</param>
/// <param name="RowsDescription">[rowsVisD].</param>
/// <param name="Rows">Auto, 1, 2 and 3; Auto draws 2 rows in S and 3 in M and L (GEN-006).</param>
/// <param name="FollowApp">[followApp]: the same state as Auto/Fijo (PER-006).</param>
/// <param name="AlwaysVisibleRow">[showStripT].</param>
/// <param name="ProfileSelectorRow">[showTabsT].</param>
/// <param name="ProfileSelectorNote">How to reach Frequents without the selector (SEL-006), under its switch.</param>
/// <param name="ColumnsTitle">[columns].</param>
/// <param name="Columns">2, 3 and 4 with a miniature (GEN-008).</param>
/// <param name="ShowKeysTitle">[showKeys].</param>
/// <param name="ShowKeys">«Con teclas» and «Solo nombre» (GEN-008).</param>
/// <param name="SampleName">[copy], the name on the miniature tile.</param>
/// <param name="SampleKeys">The keys on the miniature tile.</param>
public sealed record LayoutModel(
    string Caption,
    string RowsTitle,
    string RowsDescription,
    ValueList<SettingOption<int>> Rows,
    SwitchItem FollowApp,
    SwitchItem AlwaysVisibleRow,
    SwitchItem ProfileSelectorRow,
    string ProfileSelectorNote,
    string ColumnsTitle,
    ValueList<SettingOption<int>> Columns,
    string ShowKeysTitle,
    ValueList<SettingOption<bool>> ShowKeys,
    string SampleName,
    string SampleKeys
);
