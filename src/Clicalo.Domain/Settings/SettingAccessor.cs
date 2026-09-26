namespace Clicalo.Domain.Settings;

/// <summary>
/// Reads, writes and repairs one leaf of <see cref="UserSettings"/> (the registry behind <see cref="SettingsSchema"/>).
/// </summary>
internal abstract class SettingAccessor
{
    protected SettingAccessor(SettingDescriptor descriptor) => Descriptor = descriptor;

    /// <summary>What the product knows about the leaf.</summary>
    public SettingDescriptor Descriptor { get; }

    /// <summary>The current value, boxed; <see langword="null"/> for an absent optional value.</summary>
    public abstract object? Read(UserSettings settings);

    /// <summary>Writes <paramref name="value"/> when it has the leaf's type and is inside its range or choices.</summary>
    public abstract SettingWriteStatus TryWrite(
        UserSettings settings,
        object? value,
        out UserSettings result
    );

    /// <summary>Brings the value inside its range or choices; valid values are kept untouched.</summary>
    public abstract UserSettings Repair(UserSettings settings, out bool changed);

    /// <summary>The value of <paramref name="source"/> copied into <paramref name="target"/>.</summary>
    public abstract UserSettings CopyFrom(UserSettings target, UserSettings source);
}
