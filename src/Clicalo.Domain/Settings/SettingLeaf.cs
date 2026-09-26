namespace Clicalo.Domain.Settings;

/// <summary>A typed <see cref="SettingAccessor"/>.</summary>
/// <typeparam name="T">Type of the value.</typeparam>
internal sealed class SettingLeaf<T> : SettingAccessor
{
    private static readonly EqualityComparer<T> Comparer = EqualityComparer<T>.Default;

    private readonly Func<UserSettings, T> _get;
    private readonly Func<UserSettings, T, UserSettings> _set;
    private readonly Func<T, T> _repair;
    private readonly bool _nullable;

    public SettingLeaf(
        SettingDescriptor descriptor,
        Func<UserSettings, T> get,
        Func<UserSettings, T, UserSettings> set,
        Func<T, T> repair,
        bool nullable
    )
        : base(descriptor)
    {
        _get = get;
        _set = set;
        _repair = repair;
        _nullable = nullable;
    }

    public override object? Read(UserSettings settings) => _get(settings);

    public override SettingWriteStatus TryWrite(
        UserSettings settings,
        object? value,
        out UserSettings result
    )
    {
        result = settings;
        T typed;
        if (value is null || ReferenceEquals(value, NoSettingValue.Instance))
        {
            if (!_nullable)
            {
                return SettingWriteStatus.WrongType;
            }

            typed = default!;
        }
        else if (value is T cast)
        {
            typed = cast;
        }
        else
        {
            return SettingWriteStatus.WrongType;
        }

        if (!Comparer.Equals(_repair(typed), typed))
        {
            return SettingWriteStatus.OutOfRange;
        }

        if (!Comparer.Equals(_get(settings), typed))
        {
            result = _set(settings, typed);
        }

        return SettingWriteStatus.Written;
    }

    public override UserSettings Repair(UserSettings settings, out bool changed)
    {
        var value = _get(settings);
        var repaired = _repair(value);
        changed = !Comparer.Equals(value, repaired);
        return changed ? _set(settings, repaired) : settings;
    }

    public override UserSettings CopyFrom(UserSettings target, UserSettings source)
    {
        var value = _get(source);
        return Comparer.Equals(_get(target), value) ? target : _set(target, value);
    }
}
