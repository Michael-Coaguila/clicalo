using System.Text.Json;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.Domain.Timing;
using Clicalo.TestKit;
using PanelSize = Clicalo.Domain.Settings.PanelSize;

namespace Clicalo.Domain.Tests.Settings;

/// <summary>
/// The settings schema (blueprint §6.3): one descriptor per leaf, the defaults of docs/02 and the catalog, the ranges
/// of GEN-* and TAC-005, the repair on load (§6.5) and which settings undo restores (DAT-006).
/// </summary>
public sealed class SettingsSchemaTests
{
    [Fact]
    [Trait("Req", "DAT-001")]
    [Trait("Req", "NFR-015")]
    public void Every_leaf_of_the_settings_has_one_descriptor()
    {
        var paths = SettingsSchema.All.Select(d => d.Path).ToArray();

        paths.Distinct(StringComparer.Ordinal).Count().ShouldBe(paths.Length);
        paths.Length.ShouldBe(CountLeaves(typeof(UserSettings)));
        paths.ShouldBe(
            typeof(SettingPaths)
                .GetFields()
                .Select(f => (string)f.GetRawConstantValue()!)
                .ToArray(),
            ignoreOrder: true
        );
    }

    [Fact]
    [Trait("Req", "DAT-001")]
    public void Every_descriptor_reads_its_default_from_the_defaults()
    {
        foreach (var descriptor in SettingsSchema.All)
        {
            SettingsSchema
                .TryRead(SettingsSchema.Defaults, descriptor.Path, out var value)
                .ShouldBeTrue();
            (value ?? SettingsSchema.NoValue).ShouldBe(descriptor.Default, descriptor.Path);
            SettingsSchema.Find(descriptor.Path).ShouldBeSameAs(descriptor);
        }

        SettingsSchema.Find("nope").ShouldBeNull();
    }

    [Fact]
    [Trait("Req", "IDI-002")]
    public void Every_descriptor_text_is_a_key_of_both_languages()
    {
        var spanish = Keys("strings.es.json");
        var english = Keys("strings.en.json");
        foreach (var descriptor in SettingsSchema.All)
        {
            foreach (
                var key in new[]
                {
                    descriptor.Label,
                    descriptor.Description,
                }.OfType<Domain.Messages.MessageKey>()
            )
            {
                spanish.ShouldContain(
                    k => k == key.Value || k.StartsWith(key.Value + "_", StringComparison.Ordinal),
                    descriptor.Path
                );
                english.ShouldContain(
                    k => k == key.Value || k.StartsWith(key.Value + "_", StringComparison.Ordinal),
                    descriptor.Path
                );
            }
        }
    }

    [Fact]
    [Trait("Req", "DAT-006")]
    public void Presentation_and_placement_are_never_undoable_and_behaviour_is()
    {
        foreach (var descriptor in SettingsSchema.All)
        {
            if (descriptor.Scope != SettingScope.Behavior)
            {
                descriptor.Undoable.ShouldBeFalse(descriptor.Path);
            }
        }

        string[] notUndoable =
        [
            SettingPaths.AiFreeLeftToday,
            SettingPaths.AiFreeResetAt,
            SettingPaths.AiApiKeyRef,
        ];
        SettingsSchema
            .All.Where(d => d.Scope == SettingScope.Behavior && !d.Undoable)
            .Select(d => d.Path)
            .ShouldBe(notUndoable, ignoreOrder: true);
        Find(SettingPaths.Theme).Scope.ShouldBe(SettingScope.Presentation);
        Find(SettingPaths.Opacity).Scope.ShouldBe(SettingScope.Presentation);
        Find(SettingPaths.Size).Scope.ShouldBe(SettingScope.Presentation);
        Find(SettingPaths.PanelPositions).Scope.ShouldBe(SettingScope.Placement);
        Find(SettingPaths.ReleaseOnAppSwitch).Undoable.ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "GEN-003")]
    [Trait("Req", "GEN-004")]
    [Trait("Req", "GEN-008")]
    [Trait("Req", "GEN-009")]
    [Trait("Req", "GEN-010")]
    [Trait("Req", "GEN-011")]
    [Trait("Req", "GEN-012")]
    public void The_defaults_are_those_of_the_catalog_and_the_prototype()
    {
        var defaults = SettingsSchema.Defaults;

        defaults.Language.ShouldBe(LangCode.Es);
        defaults.Theme.ShouldBe(ThemeChoice.Auto);
        defaults.Density.ShouldBe(PanelDensity.Full);
        defaults.Size.ShouldBe(PanelSize.Medium);
        defaults.Columns.ShouldBe(3);
        defaults.RowsPreference.ShouldBe(0);
        defaults.TextScalePercent.ShouldBe(100);
        defaults.Opacity.ShouldBe(0.92);
        defaults.AutoDim.ShouldBeTrue();
        defaults.DimTo.ShouldBe(0.35);
        defaults.Feedback.ShouldBe(new FeedbackSettings(Sound: true, Flash: true));
        defaults.LockProfile.ShouldBeFalse();
        defaults.LastProfile.ShouldBeNull();
        defaults.Dock.Side.ShouldBe(DockSide.Right);
        defaults.Dock.HandlePositions.ShouldBe(new DockHandlePositions(50, 50, 50, 50));
        defaults.Dock.PerPage.ShouldBe(5);
        defaults.KeySafety.ShouldBe(
            new KeySafetySettings(TimeSpan.FromMinutes(1), ReleaseOnAppSwitch: true)
        );
        defaults.AutoSuggestProfiles.ShouldBeTrue();
        defaults.Reliability.ShouldBe(
            new ReliabilitySettings(true, true, true, true, RunAsAdmin: false)
        );
        defaults.Updates.ShouldBe(new UpdateSettings(true, true, true, UpdateChannel.Stable));
        defaults.NoKeyboardUser.ShouldBeFalse();
        SettingsSchema.Clamp(defaults, out var changed).ShouldBeSameAs(defaults);
        changed.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "TAC-001")]
    [Trait("Req", "SEG-004")]
    [Trait("Req", "PLA-003")]
    public void The_values_taken_from_the_catalogs_are_the_generated_ones()
    {
        var touch = SettingsSchema.Defaults.Touch;
        var preset = TouchPresets.Default;

        touch.ShouldBe(
            new TouchFilterSettings(
                preset.Id,
                preset.Debounce,
                preset.HitSlopPx,
                preset.CancelMovePx,
                preset.MinContact
            )
        );
        SettingsSchema.Defaults.KeySafety.MaxHold.ShouldBe(Timings.KeySafety.AutoReleaseDefault);
        SettingsSchema.MaxHoldChoices.ShouldBe(Timings.KeySafety.AutoReleaseChoices);
        SettingsSchema.Defaults.Ai.FreeLeftToday.ShouldBe(Timings.Ai.AiFreeDailyQuota);
        SettingsSchema.AiFreeLeftToday.Max.ShouldBe(Timings.Ai.AiFreeDailyQuota);
    }

    [Fact]
    [Trait("Req", "TAC-005")]
    public void The_touch_ranges_are_those_of_the_precision_sliders()
    {
        SettingsSchema.TouchDebounceMs.ShouldBe(new SettingRange(0, 1000, 50));
        SettingsSchema.TouchHitSlopPx.ShouldBe(new SettingRange(0, 40, 2));
        SettingsSchema.TouchCancelMovePx.ShouldBe(new SettingRange(0, 80, 5));
        SettingsSchema.TouchMinContactMs.ShouldBe(new SettingRange(0, 300, 10));

        foreach (var preset in TouchPresets.All)
        {
            var settings = SettingsSchema.Defaults with
            {
                Touch = new TouchFilterSettings(
                    preset.Id,
                    preset.Debounce,
                    preset.HitSlopPx,
                    preset.CancelMovePx,
                    preset.MinContact
                ),
            };
            SettingsSchema.Clamp(settings, out var changed).ShouldBeSameAs(settings, preset.Id);
            changed.ShouldBeEmpty();
        }
    }

    public static TheoryData<string, object?, string, object?> Repairs =>
        new()
        {
            { SettingPaths.Columns, 9, SettingPaths.Columns, 4 },
            { SettingPaths.RowsPreference, -2, SettingPaths.RowsPreference, 0 },
            { SettingPaths.TextScale, 175, SettingPaths.TextScale, 150 },
            { SettingPaths.Opacity, 0.1, SettingPaths.Opacity, 0.30 },
            { SettingPaths.Opacity, double.NaN, SettingPaths.Opacity, 0.92 },
            { SettingPaths.DimTo, 0.95, SettingPaths.DimTo, 0.80 },
            { SettingPaths.Theme, (ThemeChoice)42, SettingPaths.Theme, ThemeChoice.Auto },
            { SettingPaths.Size, (PanelSize)(-1), SettingPaths.Size, PanelSize.Medium },
            { SettingPaths.DockHandleTop, 99, SettingPaths.DockHandleTop, 92 },
            { SettingPaths.DockHandleLeft, 3, SettingPaths.DockHandleLeft, 8 },
            { SettingPaths.DockPerPage, 7, SettingPaths.DockPerPage, 6 },
            { SettingPaths.DockPerPage, 100, SettingPaths.DockPerPage, 8 },
            {
                SettingPaths.TouchDebounce,
                TimeSpan.FromSeconds(3),
                SettingPaths.TouchDebounce,
                TimeSpan.FromSeconds(1)
            },
            { SettingPaths.TouchHitSlop, -4, SettingPaths.TouchHitSlop, 0 },
            {
                SettingPaths.MaxHold,
                TimeSpan.FromSeconds(50),
                SettingPaths.MaxHold,
                TimeSpan.FromSeconds(60)
            },
            {
                SettingPaths.MaxHold,
                TimeSpan.FromSeconds(100),
                SettingPaths.MaxHold,
                TimeSpan.FromSeconds(120)
            },
            { SettingPaths.MaxHold, TimeSpan.Zero, SettingPaths.MaxHold, null },
            { SettingPaths.AiFreeLeftToday, 12, SettingPaths.AiFreeLeftToday, 5 },
            {
                SettingPaths.Language,
                new LangCode(string.Empty),
                SettingPaths.Language,
                LangCode.Es
            },
            {
                SettingPaths.LastProfile,
                new ProfileId(string.Empty),
                SettingPaths.LastProfile,
                null
            },
            { SettingPaths.TouchPreset, string.Empty, SettingPaths.TouchPreset, "personal" },
        };

    [Theory]
    [Trait("Req", "DAT-003")]
    [MemberData(nameof(Repairs))]
    public void Clamp_repairs_a_value_outside_its_domain_and_reports_it(
        string path,
        object? stored,
        string expectedPath,
        object? repaired
    )
    {
        var settings = Force(SettingsSchema.Defaults, path, stored);

        var clamped = SettingsSchema.Clamp(settings, out var changed);

        changed.ShouldBe([expectedPath]);
        SettingsSchema.TryRead(clamped, path, out var value).ShouldBeTrue();
        value.ShouldBe(repaired);
    }

    [Fact]
    [Trait("Req", "DAT-003")]
    public void Clamp_gives_a_missing_group_its_defaults()
    {
        var broken = SettingsSchema.Defaults with { Feedback = null!, Dock = null! };

        var clamped = SettingsSchema.Clamp(broken, out var changed);

        changed.ShouldBe(["feedback", "dock"]);
        clamped.ShouldBe(SettingsSchema.Defaults);
    }

    [Fact]
    [Trait("Req", "GEN-009")]
    public void A_value_between_the_steps_is_valid_and_kept()
    {
        var settings = SettingsSchema.Defaults with { Opacity = 0.92, TextScalePercent = 105 };

        SettingsSchema.Clamp(settings, out var changed).ShouldBeSameAs(settings);
        changed.ShouldBeEmpty();
    }

    [Fact]
    public void Write_checks_the_type_and_the_domain()
    {
        var defaults = SettingsSchema.Defaults;

        SettingsSchema
            .Write(defaults, SettingPaths.Columns, 4, out var four)
            .ShouldBe(SettingWriteStatus.Written);
        four.Columns.ShouldBe(4);
        SettingsSchema
            .Write(defaults, SettingPaths.Columns, 3, out var same)
            .ShouldBe(SettingWriteStatus.Written);
        same.ShouldBeSameAs(defaults);
        SettingsSchema
            .Write(defaults, SettingPaths.Columns, 5, out _)
            .ShouldBe(SettingWriteStatus.OutOfRange);
        SettingsSchema
            .Write(defaults, SettingPaths.Columns, 3L, out _)
            .ShouldBe(SettingWriteStatus.WrongType);
        SettingsSchema
            .Write(defaults, SettingPaths.Columns, null, out _)
            .ShouldBe(SettingWriteStatus.WrongType);
        SettingsSchema.Write(defaults, "nope", 1, out _).ShouldBe(SettingWriteStatus.UnknownPath);
        SettingsSchema
            .Write(defaults, SettingPaths.MaxHold, null, out var never)
            .ShouldBe(SettingWriteStatus.Written);
        never.KeySafety.MaxHold.ShouldBeNull();
        SettingsSchema
            .Write(defaults, SettingPaths.MaxHold, TimeSpan.FromSeconds(45), out _)
            .ShouldBe(SettingWriteStatus.OutOfRange);
        SettingsSchema
            .Write(defaults, SettingPaths.AiApiKeyRef, SettingsSchema.NoValue, out _)
            .ShouldBe(SettingWriteStatus.Written);
        SettingsSchema
            .Write(defaults, SettingPaths.FeedbackSound, false, out var quiet)
            .ShouldBe(SettingWriteStatus.Written);
        quiet.Feedback.ShouldBe(new FeedbackSettings(Sound: false, Flash: true));
    }

    [Fact]
    [Trait("Req", "DAT-006")]
    public void Undo_restores_only_the_undoable_settings()
    {
        var before = SettingsSchema.Defaults;
        var now = before with
        {
            Theme = ThemeChoice.Dark,
            KeySafety = before.KeySafety with { ReleaseOnAppSwitch = false },
            LockProfile = true,
            Touch = before.Touch with { HitSlopPx = 24 },
        };

        var restored = SettingsSchema.WithUndoableFrom(now, before);

        restored.Theme.ShouldBe(ThemeChoice.Dark);
        restored.LockProfile.ShouldBeTrue();
        restored.KeySafety.ReleaseOnAppSwitch.ShouldBeTrue();
        restored.Touch.HitSlopPx.ShouldBe(before.Touch.HitSlopPx);
        SettingsSchema.WithUndoableFrom(before, before).ShouldBeSameAs(before);
    }

    private static SettingDescriptor Find(string path) => SettingsSchema.Find(path)!;

    private static UserSettings Force(UserSettings settings, string path, object? value) =>
        path switch
        {
            SettingPaths.Columns => settings with { Columns = (int)value! },
            SettingPaths.RowsPreference => settings with { RowsPreference = (int)value! },
            SettingPaths.TextScale => settings with { TextScalePercent = (int)value! },
            SettingPaths.Opacity => settings with { Opacity = (double)value! },
            SettingPaths.DimTo => settings with { DimTo = (double)value! },
            SettingPaths.Theme => settings with { Theme = (ThemeChoice)value! },
            SettingPaths.Size => settings with { Size = (PanelSize)value! },
            SettingPaths.DockHandleTop => settings with
            {
                Dock = settings.Dock with
                {
                    HandlePositions = settings.Dock.HandlePositions with { Top = (int)value! },
                },
            },
            SettingPaths.DockHandleLeft => settings with
            {
                Dock = settings.Dock with
                {
                    HandlePositions = settings.Dock.HandlePositions with { Left = (int)value! },
                },
            },
            SettingPaths.DockPerPage => settings with
            {
                Dock = settings.Dock with { PerPage = (int)value! },
            },
            SettingPaths.TouchDebounce => settings with
            {
                Touch = settings.Touch with { Debounce = (TimeSpan)value! },
            },
            SettingPaths.TouchHitSlop => settings with
            {
                Touch = settings.Touch with { HitSlopPx = (int)value! },
            },
            SettingPaths.TouchPreset => settings with
            {
                Touch = settings.Touch with { Preset = (string)value! },
            },
            SettingPaths.MaxHold => settings with
            {
                KeySafety = settings.KeySafety with { MaxHold = (TimeSpan?)value },
            },
            SettingPaths.AiFreeLeftToday => settings with
            {
                Ai = settings.Ai with { FreeLeftToday = (int)value! },
            },
            SettingPaths.Language => settings with { Language = (LangCode)value! },
            SettingPaths.LastProfile => settings with { LastProfile = (ProfileId?)value },
            _ => throw new ArgumentOutOfRangeException(nameof(path), path, null),
        };

    private static int CountLeaves(Type type)
    {
        var count = 0;
        foreach (var property in type.GetProperties())
        {
            var propertyType = property.PropertyType;
            var nested =
                string.Equals(
                    propertyType.Namespace,
                    typeof(UserSettings).Namespace,
                    StringComparison.Ordinal
                )
                && propertyType.IsClass
                && !propertyType.IsEnum;
            count += nested ? CountLeaves(propertyType) : 1;
        }

        return count;
    }

    private static string[] Keys(string file)
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RepoPaths.Data, "i18n", file))
        );
        return [.. document.RootElement.EnumerateObject().Select(p => p.Name)];
    }
}
