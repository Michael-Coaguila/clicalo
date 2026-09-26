using Clicalo.Domain.Catalog;
using Clicalo.Domain.Document;
using Clicalo.Domain.Duplicates;
using Clicalo.Domain.Frequents;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Migration.V1;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;

namespace Clicalo.Domain.Tests.Migration;

/// <summary>
/// A new installation's document for the converter (General with one seed shortcut, default settings) and the context
/// around it.
/// </summary>
internal static class V1Baseline
{
    public static IconRef GeneralIcon { get; } = new("home");

    public static UserDocument Document()
    {
        var seed = new Shortcut(
            new ShortcutId("seed-copy"),
            LocalizedText.Same("Copiar", LangCode.Es, LangCode.En),
            new IconRef("content_copy"),
            AutoIcon: true,
            new CategoryId("edit"),
            new TapAction(
                KeyChord.Create([new KeyStroke(KeyIds.Ctrl), new KeyStroke(KeyIds.C)]),
                []
            ),
            new ShortcutOptions(false, new HoldLimit.InheritGlobal(), false),
            Origin: null,
            PinnedFrom: null
        );
        var general = new Profile(
            ProfileId.General,
            new LocalizedText([new(LangCode.Es, "General"), new(LangCode.En, "General")]),
            GeneralIcon,
            AutoIcon: false,
            new AppBinding.Manual(),
            InjectionMode.VirtualKey,
            [seed],
            Origin: null
        );
        var library = ShortcutLibrary.CreateValidated([], [general]).Value;
        return new UserDocument(
            0,
            library,
            FrequentsState.Empty,
            DuplicatePolicy.Empty,
            SettingsSchema.Defaults,
            new OnboardingState(false)
        );
    }

    public static V1ConversionContext Context(params V1Monitor[] monitors) =>
        new(
            new SequentialIds(),
            Document(),
            [.. monitors],
            new DateTimeOffset(2026, 9, 26, 10, 0, 0, TimeSpan.Zero)
        );
}
