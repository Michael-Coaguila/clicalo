using Clicalo.Application.Ports;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Sharing;
using Clicalo.Infrastructure.Persistence;

namespace Clicalo.Infrastructure.Sharing;

/// <summary>
/// <see cref="IProfileSharing"/> over <see cref="ProfileShareCodec"/> (DAT-007): the format and its limits live there;
/// this adapter only gives them to the Control Center.
/// </summary>
public sealed class ProfileSharing : IProfileSharing
{
    private readonly ProfileShareCodec _codec;
    private readonly IIdGenerator _ids;

    /// <summary>Creates the adapter.</summary>
    /// <param name="time">Clock of <c>writtenAtUtc</c>.</param>
    /// <param name="ids">New ids for an imported profile (DAT-004).</param>
    public ProfileSharing(TimeProvider time, IIdGenerator ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        _codec = new ProfileShareCodec(time);
        _ids = ids;
    }

    /// <inheritdoc />
    public SharedProfileFile Export(Profile profile, bool includeTextsInClear)
    {
        var export = _codec.Export(profile, includeTextsInClear);
        return new SharedProfileFile(export.FileName, export.Content, export.ExcludedTexts);
    }

    /// <inheritdoc />
    public Result<SharedProfile> Import(ReadOnlyMemory<byte> utf8) =>
        ProfileShareCodec
            .Import(utf8.Span, _ids)
            .Map(read => new SharedProfile(read.Profile, read.UnavailableTexts));
}
