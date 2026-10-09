using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Settings;

namespace Clicalo.Domain.Commands;

/// <summary>
/// Changes the whole touch filter in one undoable step (TAC-001, TAC-005): a preset writes its four values and its id,
/// and moving a slider writes the value and «personal». Each value goes through its descriptor
/// (<see cref="SettingsSchema.Write"/>), so the types and ranges are those of <see cref="SetSetting"/>; consecutive
/// changes join one undo step, so dragging a slider is undone at once.
/// </summary>
/// <param name="Touch">The new touch filter.</param>
public sealed record SetTouchFilter(TouchFilterSettings Touch) : IDocumentCommand
{
    /// <summary>The coalescing key of its undo step.</summary>
    public const string CoalesceKey = SetSetting.CoalescePrefix + "touch";

    /// <inheritdoc />
    public Result<DocumentChange> Apply(UserDocument document, DomainContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (Touch is null)
        {
            return Changes.Fail(CommandFailures.SettingType(L.TouchTitle.Key));
        }

        var settings = document.Settings;
        foreach (
            var (path, value) in (ReadOnlySpan<(string, object)>)
                [
                    (SettingPaths.TouchPreset, Touch.Preset),
                    (SettingPaths.TouchDebounce, Touch.Debounce),
                    (SettingPaths.TouchHitSlop, Touch.HitSlopPx),
                    (SettingPaths.TouchCancelMove, Touch.CancelMovePx),
                    (SettingPaths.TouchMinContact, Touch.MinContact),
                ]
        )
        {
            var label = SettingsSchema.Find(path)!.Label;
            switch (SettingsSchema.Write(settings, path, value, out var written))
            {
                case SettingWriteStatus.WrongType:
                case SettingWriteStatus.UnknownPath:
                    return Changes.Fail(CommandFailures.SettingType(label));
                case SettingWriteStatus.OutOfRange:
                    return Changes.Fail(CommandFailures.SettingRange(label));
            }

            settings = written;
        }

        return Changes.Recorded(
            ReferenceEquals(settings, document.Settings)
                ? document
                : document with
                {
                    Settings = settings,
                },
            L.TouchTitle,
            CoalesceKey
        );
    }
}
