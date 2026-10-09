using Clicalo.Application.Localization;
using Clicalo.Application.Tests.Store;
using Clicalo.Domain.Commands;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;

namespace Clicalo.Application.Tests.Localization;

/// <summary>
/// The hot language switch (IDI-001): the document's <c>lang</c> rules the interface language, and every registered
/// window repaints on its own thread, without restarting.
/// </summary>
[Trait("Req", "IDI-001")]
public sealed class LanguageFollowerTests
{
    private readonly StoreHarness _harness = new(StoreSamples.Document());
    private readonly LocalizationContext _localization = I18nRepository.Context();

    [Fact]
    public void It_starts_in_the_language_of_the_document()
    {
        _harness.Store.Dispatch(new SetSetting(SettingPaths.Language, LangCode.En));

        using var follower = new LanguageFollower(_localization, _harness.Store);

        _localization.Current.Locale.Code.ShouldBe("en");
    }

    [Fact]
    public void Choosing_a_language_writes_the_setting_and_repaints_every_window_on_its_thread()
    {
        using var follower = new LanguageFollower(_localization, _harness.Store);
        var queued = new List<Action>();
        var painted = new List<string>();
        using var panel = follower.Register(
            () => painted.Add("panel:" + _localization.Current.Format(L.Undo)),
            queued.Add
        );
        using var controlCenter = follower.Register(
            () => painted.Add("cc:" + _localization.Current.Format(L.Undo)),
            queued.Add
        );

        follower.Choose(LangCode.En).IsSuccess.ShouldBeTrue();

        _harness.Store.Current.Settings.Language.ShouldBe(LangCode.En);
        _harness.Store.CanUndo.ShouldBeFalse();
        painted.ShouldBeEmpty();
        queued.Count.ShouldBe(2);
        queued.ForEach(static action => action());
        painted.ShouldBe(["panel:Undo", "cc:Undo"]);
    }

    [Fact]
    public void Another_change_of_the_document_does_not_repaint_and_a_closed_window_is_left_out()
    {
        using var follower = new LanguageFollower(_localization, _harness.Store);
        var repaints = 0;
        var closed = follower.Register(() => repaints++, static action => action());

        _harness.Store.Dispatch(new SetSetting(SettingPaths.AutoDim, false));
        repaints.ShouldBe(0);

        closed.Dispose();
        follower.Choose(LangCode.En);
        repaints.ShouldBe(0);
        _localization.Current.Locale.Code.ShouldBe("en");
    }

    [Fact]
    public void A_disposed_follower_no_longer_switches()
    {
        var follower = new LanguageFollower(_localization, _harness.Store);
        follower.Dispose();

        _harness.Store.Dispatch(new SetSetting(SettingPaths.Language, LangCode.En));

        _localization.Current.Locale.Code.ShouldBe("es");
    }
}
