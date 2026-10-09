using Clicalo.Application.UseCases.Editor;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Commands;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Settings;
using Clicalo.Domain.Touch;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clicalo.Presentation.ControlCenter.TouchPrecision;

/// <summary>
/// «Precisión táctil» (docs/05 §4, TAC-001, TAC-005, TAC-006): the four presets, the four sliders and the test zone,
/// which judges every touch with the same filter as the panel (<see cref="TouchTestZone"/>). Choosing a preset writes
/// its values; moving a slider writes the value and switches to Personal; choosing Personal keeps the values. Every
/// change is one undoable step (<see cref="SetTouchFilter"/>) that the panel applies at once. Lives on the UI thread of
/// the Workspace role.
/// </summary>
public sealed class TouchPrecisionViewModel : ObservableObject
{
    private const string SavedIcon = "check";

    private readonly TouchPrecisionServices _s;
    private readonly TouchTestZone _zone;
    private TouchScreen _screen;

    /// <summary>Creates the section; it follows the document and the language by itself.</summary>
    /// <param name="services">What it works with.</param>
    public TouchPrecisionViewModel(TouchPrecisionServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _s = services;
        _zone = new TouchTestZone(Filter(Touch));
        _screen = Project();
        services.Store.Changed += (_, _) => services.Post(Refresh);
        services.Localization.LanguageChanged += (_, _) => services.Post(Refresh);
    }

    /// <summary>A message for the status bar of the Control Center (CCM-003).</summary>
    public event EventHandler<WorkspaceNoticeEventArgs>? Noticed;

    /// <summary>What the section shows.</summary>
    public TouchScreen Screen
    {
        get => _screen;
        private set => SetProperty(ref _screen, value);
    }

    private TouchFilterSettings Touch => _s.Store.Current.Settings.Touch;

    /// <summary>The touch filter values of the settings, as the panel uses them (TAC-002).</summary>
    /// <param name="touch">The touch settings.</param>
    public static TouchSettings Filter(TouchFilterSettings touch)
    {
        ArgumentNullException.ThrowIfNull(touch);
        return new TouchSettings(
            touch.Debounce,
            touch.HitSlopPx,
            touch.CancelMovePx,
            touch.MinContact
        );
    }

    /// <summary>Projects the section again (settings, language, test zone).</summary>
    public void Refresh()
    {
        _zone.Configure(Filter(Touch));
        Screen = Project();
    }

    /// <summary>
    /// A preset card (TAC-001): a preset writes its four values; Personal keeps the current values (TAC-005).
    /// </summary>
    /// <param name="id">The preset id, or <c>personal</c>.</param>
    public void ChoosePreset(string id)
    {
        var touch = Touch;
        if (string.Equals(id, touch.Preset, StringComparison.Ordinal))
        {
            return;
        }

        if (TouchPresets.Find(id) is { } preset)
        {
            Write(
                new TouchFilterSettings(
                    preset.Id,
                    preset.Debounce,
                    preset.HitSlopPx,
                    preset.CancelMovePx,
                    preset.MinContact
                )
            );
        }
        else if (string.Equals(id, SettingsSchema.PersonalTouchPreset, StringComparison.Ordinal))
        {
            Write(touch with { Preset = SettingsSchema.PersonalTouchPreset });
        }
    }

    /// <summary>
    /// A slider moved, or its − / + (TAC-005): the value is kept inside its range and the preset becomes Personal.
    /// </summary>
    /// <param name="value">Which value.</param>
    /// <param name="amount">The new value, in milliseconds or logical pixels.</param>
    public void SetValue(TouchValue value, double amount)
    {
        var touch = Touch;
        var range = RangeOf(value);
        var next = (int)Math.Round(range.Clamp(amount), MidpointRounding.AwayFromZero);
        if (next == (int)Read(touch, value))
        {
            return;
        }

        var personal = touch with { Preset = SettingsSchema.PersonalTouchPreset };
        Write(
            value switch
            {
                TouchValue.Debounce => personal with { Debounce = TimeSpan.FromMilliseconds(next) },
                TouchValue.HitSlop => personal with { HitSlopPx = next },
                TouchValue.CancelMove => personal with { CancelMovePx = next },
                _ => personal with { MinContact = TimeSpan.FromMilliseconds(next) },
            }
        );
    }

    /// <summary>Where the «Toca aquí» targets are now (layout or monitor changed).</summary>
    /// <param name="bounds">Their bounds, in physical screen pixels.</param>
    /// <param name="dpiScale">Physical pixels per logical pixel.</param>
    public void PlaceTargets(IReadOnlyList<PhysicalRect> bounds, double dpiScale) =>
        _zone.SetTargets(bounds, dpiScale);

    /// <summary>A contact of the test zone (TAC-006), judged with the panel's filter (TAC-002).</summary>
    /// <param name="pointerId">The contact.</param>
    /// <param name="phase">What it did.</param>
    /// <param name="at">Where, in physical screen pixels.</param>
    public void TestContact(uint pointerId, PointerPhase phase, PhysicalPoint at)
    {
        if (_zone.Feed(pointerId, phase, at, _s.Time.GetUtcNow()))
        {
            Screen = Project();
        }
    }

    /// <summary>[testReset]: the counters start again.</summary>
    public void ResetCounters()
    {
        _zone.Reset();
        Screen = Project();
    }

    private static SettingRange RangeOf(TouchValue value) =>
        value switch
        {
            TouchValue.Debounce => SettingsSchema.TouchDebounceMs,
            TouchValue.HitSlop => SettingsSchema.TouchHitSlopPx,
            TouchValue.CancelMove => SettingsSchema.TouchCancelMovePx,
            _ => SettingsSchema.TouchMinContactMs,
        };

    private static double Read(TouchFilterSettings touch, TouchValue value) =>
        value switch
        {
            TouchValue.Debounce => touch.Debounce.TotalMilliseconds,
            TouchValue.HitSlop => touch.HitSlopPx,
            TouchValue.CancelMove => touch.CancelMovePx,
            _ => touch.MinContact.TotalMilliseconds,
        };

    private static (Message Label, Message Description)? PresetTexts(string id) =>
        id switch
        {
            "standard" => (L.PStd, L.DStd),
            "mild-tremor" => (L.PLeve, L.DLeve),
            "strong-tremor" => (L.PFuerte, L.DFuerte),
            _ => null,
        };

    private void Write(TouchFilterSettings touch)
    {
        if (!_s.Store.Dispatch(new SetTouchFilter(touch)).IsSuccess)
        {
            return;
        }

        Refresh();
        Noticed?.Invoke(
            this,
            new WorkspaceNoticeEventArgs(
                new WorkspaceNotice(L.Saved, SavedIcon, _s.Store.CanUndo, false)
            )
        );
    }

    private string T(Message message) => _s.Localization.Current.Format(message);

    private TouchScreen Project()
    {
        var touch = Touch;
        var presets = new List<PresetOption>();
        foreach (var preset in TouchPresets.All)
        {
            if (PresetTexts(preset.Id) is { } texts)
            {
                presets.Add(
                    new PresetOption(
                        preset.Id,
                        T(texts.Label),
                        T(texts.Description),
                        string.Equals(preset.Id, touch.Preset, StringComparison.Ordinal)
                    )
                );
            }
        }

        presets.Add(
            new PresetOption(
                SettingsSchema.PersonalTouchPreset,
                T(L.PCustom),
                T(L.DCustom),
                !presets.Exists(p => p.Selected)
            )
        );
        return new TouchScreen(
            T(L.TouchTitle),
            T(L.TouchSub),
            [.. presets],
            [
                Slider(touch, TouchValue.Debounce, L.SDeb, L.SDebD, milliseconds: true),
                Slider(touch, TouchValue.HitSlop, L.SHit, L.SHitD, milliseconds: false),
                Slider(touch, TouchValue.CancelMove, L.SMov, L.SMovD, milliseconds: false),
                Slider(touch, TouchValue.MinContact, L.SMin, L.SMinD, milliseconds: true),
            ],
            Test()
        );
    }

    private TouchSlider Slider(
        TouchFilterSettings touch,
        TouchValue value,
        Message label,
        Message description,
        bool milliseconds
    )
    {
        var range = RangeOf(value);
        var current = Read(touch, value);
        var count = (long)Math.Round(current, MidpointRounding.AwayFromZero);
        var name = T(label);
        return new TouchSlider(
            value,
            name,
            T(description),
            current,
            count == 0 ? T(L.Off)
                : milliseconds ? T(L.MsValue(count))
                : T(L.PxValue(count)),
            range.Min,
            range.Max,
            range.Step,
            T(L.LessOf(name: name)),
            T(L.MoreOf(name: name))
        );
    }

    private TestZoneModel Test()
    {
        var last = _zone.Last;
        return new TestZoneModel(
            T(L.TestZone),
            T(L.TapHere),
            [.. _zone.Marks],
            _zone.Registered,
            T(L.Registered),
            _zone.Ignored,
            T(L.Ignored),
            T(
                last switch
                {
                    TestOutcome.Registered => L.TOk,
                    TestOutcome.TooShort => L.TShort,
                    TestOutcome.TooSoon => L.TDouble,
                    TestOutcome.Moved => L.TMoved,
                    _ => L.TestIdle,
                }
            ),
            last is { } outcome && outcome != TestOutcome.Registered,
            T(L.TestReset)
        );
    }
}
