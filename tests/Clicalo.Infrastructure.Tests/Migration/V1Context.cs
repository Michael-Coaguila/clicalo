using System.Globalization;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Document;
using Clicalo.Domain.Duplicates;
using Clicalo.Domain.Frequents;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Migration.V1;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;

namespace Clicalo.Infrastructure.Tests.Migration;

/// <summary>
/// The conversion context of the import tests: deterministic ids, a new installation's document (General only) and the
/// user's touch screen. Building the document needs the domain package: use it inside <see cref="DomainPending"/>.
/// </summary>
internal static class V1Context
{
    public static V1Monitor TouchScreen { get; } =
        new(@"\\.\DISPLAY1", 0, 0, 2400, 1520, 1.75, true);

    /// <summary>A context for paths that fail before converting: its document is missing, so a conversion would fail loudly.</summary>
    public static V1ConversionContext BeforeConversion() =>
        new(new SequentialIds(), null!, [TouchScreen], DateTimeOffset.UnixEpoch);

    public static V1ConversionContext Create() =>
        new(
            new SequentialIds(),
            Baseline(),
            [TouchScreen],
            new DateTimeOffset(2026, 9, 26, 10, 0, 0, TimeSpan.Zero)
        );

    private static UserDocument Baseline()
    {
        var general = new Profile(
            ProfileId.General,
            LocalizedText.Same("General", LangCode.Es, LangCode.En),
            new IconRef("home"),
            AutoIcon: false,
            new AppBinding.Manual(),
            InjectionMode.VirtualKey,
            [],
            Origin: null
        );
        return new UserDocument(
            0,
            ShortcutLibrary.CreateValidated([], [general]).Value,
            FrequentsState.Empty,
            DuplicatePolicy.Empty,
            SettingsSchema.Defaults,
            new OnboardingState(false)
        );
    }

    private sealed class SequentialIds : IIdGenerator
    {
        private int _profiles;
        private int _shortcuts;

        public ProfileId NewProfileId() =>
            new("p" + (++_profiles).ToString(CultureInfo.InvariantCulture));

        public ShortcutId NewShortcutId() =>
            new("s" + (++_shortcuts).ToString(CultureInfo.InvariantCulture));
    }
}
