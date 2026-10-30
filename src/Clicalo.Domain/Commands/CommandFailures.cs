using Clicalo.Domain.Errors;
using Clicalo.Domain.Messages;

namespace Clicalo.Domain.Commands;

/// <summary>
/// The expected failures of the document commands (their own; library failures pass through). Codes are stable; the
/// messages reuse existing keys of <c>data/i18n</c> until the dedicated texts requested for M2 land
/// (docs/testing/spikes/M2-ownership.md rule 5).
/// </summary>
internal static class CommandFailures
{
    public const string BlankDraftCode = "command.draft.blank";
    public const string NotBlankDraftCode = "command.draft.not_blank";
    public const string ShortcutNotFoundCode = "command.shortcut.not_found";
    public const string NotInAlwaysVisibleCode = "command.always.not_there";
    public const string NotMacroCode = "command.macro.not_macro";
    public const string StepNotFoundCode = "command.macro.step_not_found";
    public const string NotRepeatedCode = "command.duplicate.not_repeated";
    public const string UnknownSettingCode = "command.setting.unknown";
    public const string SettingTypeCode = "command.setting.wrong_type";
    public const string SettingRangeCode = "command.setting.out_of_range";
    public const string MonitorEmptyCode = "command.position.monitor_empty";
    public const string InvalidBackupCode = "command.backup.invalid";
    public const string IdsExhaustedCode = "command.ids.exhausted";
    public const string NotAMergeCode = "command.import.not_a_merge";

    public static Failure BlankDraft() => Warning(BlankDraftCode, L.Incomplete);

    public static Failure NotBlankDraft() => Warning(NotBlankDraftCode, L.Incomplete);

    public static Failure ShortcutNotFound() => Warning(ShortcutNotFoundCode, L.ItemGone);

    public static Failure NotInAlwaysVisible() => Warning(NotInAlwaysVisibleCode, L.PinAll2);

    public static Failure NotMacro() => Warning(NotMacroCode, L.Steps);

    public static Failure StepNotFound() => Warning(StepNotFoundCode, L.Steps);

    public static Failure NotRepeated() => Warning(NotRepeatedCode, L.DupSummary);

    public static Failure UnknownSetting() => Warning(UnknownSettingCode, L.Saved);

    public static Failure SettingType(MessageKey label) =>
        Warning(SettingTypeCode, L.SettingInvalid(new Message(label)));

    public static Failure SettingRange(MessageKey label) =>
        Warning(SettingRangeCode, L.SettingInvalid(new Message(label)));

    public static Failure MonitorEmpty() => Warning(MonitorEmptyCode, L.Move);

    public static Failure InvalidBackup() =>
        new(
            InvalidBackupCode,
            L.BackupDamaged,
            FailureSeverity.Warning,
            FailureRecovery.RestoreBackup,
            FailureAnnouncement.Polite
        );

    public static Failure IdsExhausted() => Warning(IdsExhaustedCode, L.Retry);

    public static Failure NotAMerge() => Warning(NotAMergeCode, L.ImportInvalid);

    private static Failure Warning(string code, Message message) =>
        new(
            code,
            message,
            FailureSeverity.Warning,
            FailureRecovery.None,
            FailureAnnouncement.Polite
        );
}
