using Clicalo.Domain.Frequents;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Timing;
using Clicalo.Windowing.IntegrationTests.SearchPanel;

namespace Clicalo.Windowing.IntegrationTests.Interactions;

/// <summary>
/// Edit mode headless (CUA-012, CUA-013, REG-04, REG-07): the sticky hint, a tap that opens the editor, the × that
/// arms for 3.5 s and deletes with undo on the second tap, Frequents and the search, and «+ Añadir».
/// </summary>
public sealed class EditModeViewModelTests
{
    private readonly InteractionsWorld _world = new();

    [Fact]
    [Trait("Req", "CUA-012")]
    public void Entering_shows_the_sticky_hint_and_leaving_removes_it()
    {
        var edit = _world.EditMode;

        edit.Enter();

        edit.IsOn.ShouldBeTrue();
        edit.ShowsRemove.ShouldBeTrue();
        edit.ShowsAdd.ShouldBeTrue();
        _world
            .Text(_world.Notices.Sticky!)
            .ShouldBe("Toca un botón para editarlo o × para quitarlo");

        edit.Toggle();

        edit.IsOn.ShouldBeFalse();
        edit.ShowsRemove.ShouldBeFalse();
        _world.Notices.Sticky.ShouldBeNull();
    }

    [Fact]
    [Trait("Req", "CUA-012")]
    [Trait("Req", "EJE-001")]
    public void In_edit_mode_a_tap_opens_the_editor_and_runs_nothing()
    {
        _world.EditMode.OnTap(InteractionsWorld.Bold).ShouldBeFalse();
        _world.EditMode.Enter();

        _world.EditMode.OnTap(InteractionsWorld.Bold).ShouldBeTrue();

        _world.ControlCenter.Requests.ShouldBe(["editor:bold"]);
        _world.Engine.Count.ShouldBe(0);
    }

    [Fact]
    [Trait("Req", "CUA-012")]
    [Trait("Req", "REG-04")]
    [Trait("Req", "REG-07")]
    public void The_cross_arms_on_the_first_tap_and_deletes_with_undo_on_the_second()
    {
        var edit = _world.EditMode;
        edit.Enter();

        edit.Remove(InteractionsWorld.Bold);

        edit.Armed.ShouldBe(InteractionsWorld.Bold);
        edit.ConfirmText.ShouldBe("Confirmar");
        _world.Store.Current.Library.TryGetShortcut(InteractionsWorld.Bold, out _).ShouldBeTrue();
        _world.Notices.Timed.ShouldBeEmpty();

        edit.Remove(InteractionsWorld.Bold);

        edit.Armed.ShouldBeNull();
        _world.Store.Current.Library.TryGetShortcut(InteractionsWorld.Bold, out _).ShouldBeFalse();
        var notice = _world.Notices.Timed.Single();
        _world.Text(notice).ShouldBe("Atajo eliminado");
        notice.CanUndo.ShouldBeTrue();

        _world.Store.Undo().IsSuccess.ShouldBeTrue();
        _world.Store.Current.Library.TryGetShortcut(InteractionsWorld.Bold, out _).ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "CUA-012")]
    [Trait("Req", "REG-04")]
    public void An_armed_cross_disarms_after_three_and_a_half_seconds()
    {
        var edit = _world.EditMode;
        edit.Enter();
        edit.Remove(InteractionsWorld.Bold);

        _world.Time.Advance(Timings.Confirmation.DestructiveConfirmWindow);

        edit.Armed.ShouldBeNull();
        edit.Remove(InteractionsWorld.Bold);
        edit.Armed.ShouldBe(InteractionsWorld.Bold);
        _world.Store.Current.Library.TryGetShortcut(InteractionsWorld.Bold, out _).ShouldBeTrue();
        Timings.Confirmation.DestructiveConfirmWindow.ShouldBe(TimeSpan.FromSeconds(3.5));
    }

    [Fact]
    [Trait("Req", "CUA-012")]
    public void Arming_another_cross_or_leaving_disarms_the_first()
    {
        var edit = _world.EditMode;
        edit.Enter();
        edit.Remove(InteractionsWorld.Bold);
        edit.Remove(InteractionsWorld.Dictation);

        edit.Armed.ShouldBe(InteractionsWorld.Dictation);
        edit.Remove(InteractionsWorld.Bold);
        edit.Armed.ShouldBe(InteractionsWorld.Bold);
        _world.Store.Current.Library.TryGetShortcut(InteractionsWorld.Bold, out _).ShouldBeTrue();

        edit.Exit();
        edit.Armed.ShouldBeNull();
        _world.Confirm.ArmedSubject.ShouldBeNull();
    }

    [Fact]
    [Trait("Req", "CUA-013")]
    [Trait("Req", "FRE-003")]
    public void In_frequents_the_cross_removes_from_frequents_and_there_is_no_add_tile()
    {
        var edit = _world.EditMode;
        edit.Enter();
        edit.ApplyView(frequents: true, searching: false, ProfileId.General);

        edit.Removal.ShouldBe(TileRemoval.HideFromFrequents);
        edit.ShowsAdd.ShouldBeFalse();
        edit.RemoveNameOf("Negrita").ShouldBe("Quitar Negrita de Frecuentes");

        edit.Remove(InteractionsWorld.Bold);
        edit.Remove(InteractionsWorld.Bold);

        _world.Store.Current.Library.TryGetShortcut(InteractionsWorld.Bold, out _).ShouldBeTrue();
        _world.Store.Current.Frequents.Hidden.ShouldBe([InteractionsWorld.Bold]);
        _world.Text(_world.Notices.Timed.Single()).ShouldBe("Quitado de Frecuentes");
        _world.Notices.Timed.Single().CanUndo.ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "CUA-013")]
    public void In_the_search_there_is_no_cross_and_no_add_tile()
    {
        var edit = _world.EditMode;
        edit.Enter();
        edit.ApplyView(frequents: false, searching: true, SearchTestWorld.Word);

        edit.ShowsRemove.ShouldBeFalse();
        edit.ShowsAdd.ShouldBeFalse();
        edit.Remove(InteractionsWorld.Bold);
        edit.Armed.ShouldBeNull();
    }

    [Fact]
    [Trait("Req", "CUA-012")]
    [Trait("Req", "IDI-001")]
    public void The_add_tile_opens_the_library_of_the_profile_in_view_and_names_are_localized()
    {
        var edit = _world.EditMode;
        edit.Enter();
        edit.ApplyView(frequents: false, searching: false, SearchTestWorld.Word);

        edit.Add();

        _world.ControlCenter.Requests.ShouldBe(["library:" + SearchTestWorld.Word.Value]);
        edit.AddText.ShouldBe("Añadir");
        edit.RemoveNameOf("Negrita").ShouldBe("Eliminar Negrita");

        _world.Localization.TrySetLanguage("en");
        edit.Relocalize();
        edit.AddText.ShouldBe("Add");
        edit.ConfirmText.ShouldBe("Confirm");
        edit.RemoveNameOf("Bold").ShouldBe("Delete Bold");
    }
}
