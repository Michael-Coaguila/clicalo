using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;

namespace Clicalo.Domain.Commands;

/// <summary>
/// Changes one setting through its descriptor (blueprint §6.3). A Behavior setting records an undo step, and
/// consecutive changes of the same setting join it; presentation and placement settings do not enter the history,
/// because the same control reverts them (DAT-006, <c>undo-exemptions.json</c>: <c>descriptorNotUndoable</c>). The
/// value must have the setting's type and be inside its range or choices (<see cref="SettingsSchema.Write"/>); a last
/// profile must exist.
/// </summary>
/// <param name="Path">A <see cref="SettingPaths"/> value.</param>
/// <param name="Value">The new value; <see langword="null"/> clears an optional setting.</param>
public sealed record SetSetting(string Path, object? Value) : IDocumentCommand
{
    /// <summary>The prefix of the coalescing key of a setting's undo step.</summary>
    public const string CoalescePrefix = "setting:";

    /// <inheritdoc />
    public Result<DocumentChange> Apply(UserDocument document, DomainContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (SettingsSchema.Find(Path) is not { } descriptor)
        {
            return Changes.Fail(CommandFailures.UnknownSetting());
        }

        if (
            string.Equals(Path, SettingPaths.LastProfile, StringComparison.Ordinal)
            && Value is ProfileId last
            && !document.Library.TryGetProfile(last, out _)
        )
        {
            return Changes.Fail(CommandFailures.SettingRange(descriptor.Label));
        }

        switch (SettingsSchema.Write(document.Settings, Path, Value, out var settings))
        {
            case SettingWriteStatus.WrongType:
                return Changes.Fail(CommandFailures.SettingType(descriptor.Label));
            case SettingWriteStatus.OutOfRange:
                return Changes.Fail(CommandFailures.SettingRange(descriptor.Label));
        }

        var next = ReferenceEquals(settings, document.Settings)
            ? document
            : document with
            {
                Settings = settings,
            };
        return descriptor.Undoable
            ? Results.Ok(
                new DocumentChange(
                    next,
                    [],
                    new UndoIntent.Record(descriptor.Label, CoalescePrefix + Path),
                    new BackupRequirement.None()
                )
            )
            : Changes.Transparent(next);
    }
}
